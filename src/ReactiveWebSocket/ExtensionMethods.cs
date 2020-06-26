using Microsoft;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
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
                catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken) { /* Unsubscribed */ }
                catch (OperationCanceledException ex)
                {
                    observer.OnError(ex);
                }
                catch (RxWebSocketException ex)
                {
                    observer.OnError(ex);
                }

                return Disposable.Empty;
            });
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
                            cancellationToken).ConfigureAwait(false);
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
            Requires.NotNull(channelWriter, nameof(channelWriter));
            Requires.NotNull(socket, nameof(socket));

            var bufferWriter = new ArrayBufferWriter<byte>();

            while (await ReadMessageAsync().ConfigureAwait(false))
            { }

            async Task<bool> ReadMessageAsync()
            {
                ValueWebSocketReceiveResult result;

                do
                {
                    result = await socket.ReceiveAsync(bufferWriter.GetMemory(), cancellationToken).ConfigureAwait(false);

                    switch (result.MessageType)
                    {
                        case WebSocketMessageType.Text:
                        case WebSocketMessageType.Binary:
                            bufferWriter.Advance(result.Count);
                            break;
                        case WebSocketMessageType.Close:
                            return false;
                    }
                } while (!result.EndOfMessage);

                channelWriter.TryWrite(new Message(Convert(result.MessageType), bufferWriter.WrittenSpan.ToArray()));
                bufferWriter.Clear();

                return true;

                static MessageType Convert(WebSocketMessageType type) =>
                    type switch
                    {
                        WebSocketMessageType.Text => MessageType.Text,
                        WebSocketMessageType.Binary => MessageType.Binary,
                        _ => throw new InvalidOperationException($"Expected web socket message type {WebSocketMessageType.Text} or {WebSocketMessageType.Binary}, but received {type}.")
                    };
            }
        }
    }
}
