using NSubstitute;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveWebSocket.UnitTests
{
    internal static class ExtensionMethods
    {
        public static Action ReceiveNormalClosure(this WebSocket mock)
        {
            var receiveResultSource = new TaskCompletionSource<ValueWebSocketReceiveResult>();

            mock.ReceiveAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<ValueWebSocketReceiveResult>(receiveResultSource.Task));

            return () =>
            {
                mock.State.Returns(WebSocketState.Closed);
                mock.CloseStatus.Returns(WebSocketCloseStatus.NormalClosure);
                receiveResultSource.SetResult(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            };
        }
    }
}
