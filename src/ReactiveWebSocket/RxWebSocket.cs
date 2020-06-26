using Microsoft;
using NSpecifications;
using ReactiveWebSocket.State.Inputs;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ReactiveWebSocket
{
    public sealed class RxWebSocket : IRxWebSocket, IDisposable
    {
        private const string SOCKET_READ_ERROR = "An error occured when reading from socket.";
        private const string SOCKET_WRITE_ERROR = "An error occured when writing to socket.";

        private readonly Channel<Input> inputChannel = Channel.CreateUnbounded<Input>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = false });
        private readonly Channel<Message> receiveChannel;
        private readonly Channel<Message> sendChannel;
        private readonly TaskCompletionSource<bool> sendCompletionSource = new TaskCompletionSource<bool>();
        private int isDisposed = 0;
        private State state;
        private readonly Task eventLoop;
        private readonly TaskCompletionSource<bool> closeCompletionSource = new TaskCompletionSource<bool>();

        /// <summary>
        /// </summary>
        /// <param name="singleReceiver">true readers from the channel guarantee that there will only ever be at most one read operation at a time; false if no such constraint is guaranteed.</param>
        /// <param name="singleSender">true if writers to the channel guarantee that there will only ever be at most one write operation at a time; false if no such constraint is guaranteed.</param>
        public RxWebSocket(WebSocket webSocket, bool singleReceiver = true, bool singleSender = true)
        {
            Requires.NotNull(webSocket, nameof(webSocket));

            if (webSocket is null)
            {
                throw new ArgumentNullException(nameof(webSocket));
            }

            var webSocketState = webSocket.State;

            if (webSocketState != WebSocketState.Open)
            {
                throw new ArgumentException($"Web socket must be connected and open, but is in state {webSocketState}.", nameof(webSocket));
            }

            this.receiveChannel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions() { SingleWriter = true, SingleReader = singleReceiver });
            this.sendChannel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions() { SingleReader = true, SingleWriter = singleSender });
            this.SendCompletion = this.sendCompletionSource.Task;
            this.eventLoop = EventLoop();
            this.state = new Open(this, webSocket);
        }

        /// <exception cref="RxWebSocketException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public ChannelReader<Message> Receiver => this.receiveChannel.Reader;

        public ChannelWriter<Message> Sender => this.sendChannel.Writer;

        public Task SendCompletion { get; }

        /// <summary>
        /// Close web socket gracefully. SendCompletion must be completed before closing.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException">Thrown if SendCompletion is not completed.</exception>
        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            if (!this.SendCompletion.IsCompleted)
            {
                throw new InvalidOperationException("Complete Sender and await SendCompletion before calling CloseAsync().");
            }

            this.PostInput(new CloseAsync(cancellationToken));
            return Task.Run(async () => await this.closeCompletionSource.Task.ConfigureAwait(false));
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref this.isDisposed, 1, 0) == 0)
            {
                this.PostInput(new Dispose());
                this.eventLoop.GetAwaiter().GetResult();

                this.eventLoop.Dispose();

                if (this.state is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private void PostInput(Input input) => this.inputChannel.Writer.TryWrite(input);

        private async Task EventLoop()
        {
            while (await this.inputChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (this.inputChannel.Reader.TryRead(out var input))
                {
                    await ChangeState(input).ConfigureAwait(false);

                    if (this.state is Closed)
                    {
                        this.inputChannel.Writer.Complete();
                        return;
                    }
                }
            }

            async Task ChangeState(Input input)
            {
                this.state = (this.state, input) switch
                {
                    (Open open, Dispose dispose) => ((Func<State>)(() =>
                    {
                        open.Dispose();
                        return new Disposed();
                    }))(),
                    (Open open, CloseReceived closeReceived) => ((Func<State>)(() =>
                    {
                        open.Dispose();
                        return new ClosedNormally();
                    }))(),
                    (Open open, ReceiveError receiveError) => ((Func<State>)(() =>
                    {
                        open.Dispose();
                        return new Faulted();
                    }))(),
                    (Open open, SendError sendError) => ((Func<State>)(() =>
                    {
                        open.Dispose();
                        return new Faulted();
                    }))(),
                    (Open open, CloseAsync closeAsync) when this.SendCompletion.IsCompleted => await ((Func<Task<State>>)(async () =>
                    {
                        try
                        {
                            await open.CloseAsync(closeAsync.Token).ConfigureAwait(false);

                            open.Dispose();

                            return new ClosedNormally();
                        }
                        catch (OperationCanceledException)
                        {
                            open.Dispose();
                            return new Aborted();
                        }
                        catch (WebSocketException)
                        {
                            open.Dispose();
                            return new Faulted();
                        }
                    }))().ConfigureAwait(false),

                    _ => throw new InvalidTransitionException(this.state.GetType(), input.GetType()),
                };
            }
        }

        internal static ASpec<WebSocket> NormalClosure { get; } = new Spec<WebSocket>(s => s.CloseStatus.HasValue && s.CloseStatus.Value == WebSocketCloseStatus.NormalClosure);

        internal abstract class State
        {
        }

        internal sealed class Open : State, IDisposable
        {
            private readonly RxWebSocket parent;
            private readonly WebSocket socket;
            private readonly CancellationTokenSource cts = new CancellationTokenSource();
            private readonly Task receiveLoopTask;
            private readonly Task sendLoopTask;
            private readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

            public Open(RxWebSocket rxWebSocket, WebSocket socket)
            {
                this.parent = rxWebSocket;
                this.socket = socket;

                this.receiveLoopTask = this.InitReceiveLoop();
                this.sendLoopTask = this.InitSendLoop();
            }

            /// <summary>
            /// Closes websocket gracefully, waiting for all messages to be sent before calling CloseOutputAsync on it.
            /// </summary>
            /// <exception cref="OperationCanceledException"></exception>
            /// <exception cref="WebSocketException"></exception>
            public async Task CloseAsync(CancellationToken cancellationToken)
            {
                await sendLoopTask.ConfigureAwait(false);
                await this.socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancellationToken).ConfigureAwait(false);
                await this.receiveLoopTask.ConfigureAwait(false);
            }

            private async Task InitReceiveLoop()
            {
                var channelWriter = this.parent.receiveChannel.Writer;

                try
                {
                    await channelWriter.ReceiveLoop(this.socket, this.cts.Token).ConfigureAwait(false);

                    Assumes.True(this.socket.CloseStatus.HasValue, "Socket has no CloseStatus");

                    if (this.socket.CloseStatus.Value == WebSocketCloseStatus.NormalClosure)
                    {
                        onSuccess();
                    }
                    else
                    {
                        onError(new BadClosureException(this.socket.CloseStatus.Value, this.socket.CloseStatusDescription));
                    }
                }
                catch (OperationCanceledException ex)
                {
                    onCancellation(ex);
                }
                catch (WebSocketException ex)
                {
                    onError(new RxWebSocketException(SOCKET_READ_ERROR, ex));
                }
                finally
                {
                    this.parent.closeCompletionSource.SetResult(true);
                }

                void onSuccess()
                {
                    channelWriter.Complete();
                    this.parent.PostInput(new CloseReceived());
                }

                void onCancellation(OperationCanceledException ex)
                {
                    channelWriter.Complete(ex);
                }

                void onError(Exception ex)
                {
                    channelWriter.Complete(ex);
                    this.parent.PostInput(new ReceiveError(ex));
                }
            }

            private async Task InitSendLoop()
            {
                try
                {
                    await this.parent.sendChannel.Reader.SendLoop(this.socket, this.semaphore, this.cts.Token).ConfigureAwait(false);
                    onSuccess();
                }
                catch (OperationCanceledException)
                {
                    onCancelled();
                }
                catch (WebSocketException error)
                {
                    onError(new RxWebSocketException(SOCKET_WRITE_ERROR, error));
                }

                void onSuccess()
                {
                    Debug.Assert(this.parent.sendChannel.Reader.Completion.IsCompletedSuccessfully);
                    this.parent.sendCompletionSource.SetResult(true);
                }

                void onCancelled()
                {
                    this.parent.sendCompletionSource.SetCanceled();
                    this.parent.Sender.TryComplete();
                }

                void onError(Exception ex)
                {
                    this.parent.sendCompletionSource.SetException(ex);
                    this.parent.Sender.TryComplete();
                    this.parent.PostInput(new SendError(ex));
                }
            }

            public void Dispose()
            {
                try
                {
                    // CancellationToken in send loop will throw before hitting socket
                    semaphore.Wait();
                    this.cts.Cancel();
                    semaphore.Release();

                    Task.WhenAll(this.sendLoopTask, this.receiveLoopTask).GetAwaiter().GetResult();
                }
                finally
                {
                    semaphore.Dispose();
                    this.cts.Dispose();
                    this.socket.Dispose();
                }
            }
        }

        internal abstract class Closed : State { }

        internal sealed class ClosedNormally : Closed { }

        internal sealed class Faulted : Closed { }

        internal sealed class Aborted : Closed { }

        internal sealed class Disposed : Closed { }
    }
}
