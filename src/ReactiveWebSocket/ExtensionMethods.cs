using NSpecifications;
using System;
using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ReactiveWebSocket
{
    public static class ExtensionMethods
    {
        public static IObservable<Message> ToObservable(this IRxWebSocket rxWebSocket)
        {
            return Observable.Create<Message>(async (observer, cancellationToken) =>
            {
                try
                {
                    while (await rxWebSocket.Receiver.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (rxWebSocket.Receiver.TryRead(out var message))
                        {
                            observer.OnNext(message);
                        }
                    }

                    observer.OnCompleted();
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
        }

        /// <summary>
        /// Creates a BadClosureException with WebSocket close status details.
        /// </summary>
        /// <param name="socket"></param>
        /// <param name="inner"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        internal static BadClosureException BadClosureException(this WebSocket socket)
        {
            Debug.Assert(socket != null);
            Debug.Assert(socket.State == WebSocketState.Closed);
            Debug.Assert(socket.CloseStatus.HasValue);
            Debug.Assert(!socket.Is(RxWebSocket.NormalClosure));

            return new BadClosureException(socket.CloseStatus!.Value, socket.CloseStatusDescription);
        }

        /// <summary>
        /// Reads messages from channel and writes them to web socket.
        /// </summary>
        /// <param name="channelReader"></param>
        /// <param name="socket"></param>
        /// <param name="cancellationToken"></param>
        /// <exception cref="WebSocketException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        /// <returns></returns>
        internal static async Task SendLoop(this ChannelReader<Message> channelReader, WebSocket socket, SemaphoreSlim semaphore, CancellationToken cancellationToken)
        {
            while (await channelReader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channelReader.TryRead(out var message))
                {
                    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await socket.SendAsync(
                            message.Data.AsMemory(),
                            message.Type == MessageType.Text ? WebSocketMessageType.Text : WebSocketMessageType.Binary,
                            true,
                            cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }
            }
        }

        /// <summary>
        /// Reads messages from web socket and writes them to channel.
        /// Complete() will be called on channel writer when a close message is received from socket.
        /// On exception or cancellation, channel writer will not be set to complete.
        /// </summary>
        /// <exception cref="WebSocketException"></exception>
        /// <exception cref="OperationCanceledException"></exception>
        public static async Task ReceiveLoop(this ChannelWriter<Message> channelWriter, WebSocket socket, CancellationToken cancellationToken)
        {
            var bufferWriter = new ArrayBufferWriter<byte>();

            while (await ReadMessageAsync(channelWriter, socket, bufferWriter, cancellationToken).ConfigureAwait(false))
            { }

            static async Task<bool> ReadMessageAsync(ChannelWriter<Message> channelWriter, WebSocket socket, ArrayBufferWriter<byte> receiveBuffer, CancellationToken cancellationToken)
            {
                ValueWebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(receiveBuffer.GetMemory(), cancellationToken);

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                        case WebSocketMessageType.Binary:
                            receiveBuffer.Advance(result.Count);
                            break;
                        case WebSocketMessageType.Close:
                            return false;
                    }
                } while (!result.EndOfMessage);

                channelWriter.TryWrite(
                    new Message(
                        result.MessageType == WebSocketMessageType.Text ? MessageType.Text : MessageType.Binary,
                        receiveBuffer.WrittenSpan.ToArray()));

                receiveBuffer.Clear();

                return true;
            }
        }
    }
}
