using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveWebSocket.UnitTests
{
    public class OnCloseReceived
    {
        [Fact]
        public void receiver_should_complete()
        {
            // Arrange
            var mock = Substitute.For<WebSocket>();
            mock.State.Returns(WebSocketState.Open);

            var receiveResultSource = new TaskCompletionSource<ValueWebSocketReceiveResult>();
            mock.ReceiveAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<ValueWebSocketReceiveResult>(receiveResultSource.Task));

            var rxWebSocket = new RxWebSocket(mock);
            rxWebSocket.Receiver.Completion.IsCompleted.ShouldBeFalse();

            // Act
            mock.State.Returns(WebSocketState.Closed);
            mock.CloseStatus.Returns(WebSocketCloseStatus.NormalClosure);
            mock.CloseStatusDescription.Returns(string.Empty);
            receiveResultSource.SetResult(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));


            // Assert
            Should.CompleteIn(rxWebSocket.Receiver.Completion, TimeSpan.FromMilliseconds(100));
        }

        [Fact]
        public void sending_should_complete()
        {
            // Arrange
            var mock = Substitute.For<WebSocket>();
            mock.State.Returns(WebSocketState.Open);

            var closeTrigger = mock.ReceiveNormalClosure();

            var rxWebSocket = new RxWebSocket(mock);
            rxWebSocket.Receiver.Completion.IsCompleted.ShouldBeFalse();

            // Act
            closeTrigger();

            mock.State.Returns(WebSocketState.Closed);
            mock.SendAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<WebSocketMessageType>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Throws(new ObjectDisposedException(null));

            Should.CompleteIn(Assert.ThrowsAsync<TaskCanceledException>(() => rxWebSocket.SendCompletion), TimeSpan.FromMilliseconds(100));

            // Assert
            rxWebSocket.Sender.TryWrite(new Message(MessageType.Text, Array.Empty<byte>())).ShouldBeFalse();
        }

        [Fact(Timeout = 200)]
        public void socket_should_be_disposed()
        {
            // Arrange
            var mock = Substitute.For<WebSocket>();
            mock.State.Returns(WebSocketState.Open);

            var receiveResultSource = new TaskCompletionSource<ValueWebSocketReceiveResult>();

            mock.ReceiveAsync(Arg.Any<Memory<byte>>(), default)
                .ReturnsForAnyArgs(new ValueTask<ValueWebSocketReceiveResult>(receiveResultSource.Task));

            var rxWebSocket = new RxWebSocket(mock);
            rxWebSocket.Receiver.Completion.IsCompleted.ShouldBeFalse();

            TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
            mock.When(s => s.Dispose()).Do(c => tcs.SetResult(true));

            // Act
            mock.State.Returns(WebSocketState.Closed);
            mock.CloseStatus.Returns(WebSocketCloseStatus.NormalClosure);
            mock.CloseStatusDescription.Returns(string.Empty);
            receiveResultSource.SetResult(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));

            Should.CompleteIn(tcs.Task, TimeSpan.FromMilliseconds(100));
            mock.Received(1).Dispose();
        }
    }
}
