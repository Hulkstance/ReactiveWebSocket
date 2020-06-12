using NSubstitute;
using Shouldly;
using System;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveWebSocket.UnitTests
{
    public class OnCloseAsync
    {
        private static readonly TimeSpan timeout = Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(1);

        private readonly CancellationTokenSource cts = new CancellationTokenSource(timeout);

        [Fact]
        public void throw_unless_sender_completed()
        {
            // Arrange
            var stub = Substitute.For<WebSocket>();
            stub.State.Returns(WebSocketState.Open);

            var receiveResultSource = new TaskCompletionSource<ValueWebSocketReceiveResult>();
            stub.ReceiveAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<ValueWebSocketReceiveResult>(receiveResultSource.Task));

            var rxSocket = new RxWebSocket(stub);

            var ex = Should.CompleteIn(
                Should.ThrowAsync<InvalidOperationException>(async () => await rxSocket.CloseAsync(this.cts.Token)),
                timeout);

            ex.Message.ShouldBe("Complete Sender and await SendCompletion before calling CloseAsync().");
        }

        [Fact]
        public async Task after_send_completion_should_succeed()
        {
            var mock = Substitute.For<WebSocket>();
            mock.State.Returns(WebSocketState.Open);

            var closeTrigger = mock.ReceiveNormalClosure();

            var closeOutputResultSource = new TaskCompletionSource<bool>();
            mock.CloseOutputAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(closeOutputResultSource.Task);

            Action closeOutputTrigger = () =>
            {
                closeOutputResultSource.SetResult(true);
                mock.State.Returns(WebSocketState.CloseSent);
                closeTrigger();
            };

            var rxSocket = new RxWebSocket(mock);
            rxSocket.Sender.Complete();

            Should.CompleteIn(rxSocket.SendCompletion, timeout);

            var closeTask = rxSocket.CloseAsync(this.cts.Token);

            closeOutputTrigger();
            Should.CompleteIn(closeTask, timeout);

            await mock.Received(1).CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, this.cts.Token);
        }

        [Fact]
        public async Task TokenCancelled_VerifySocketDisposed()
        {
            var mock = Substitute.For<WebSocket>();
            mock.State.Returns(WebSocketState.Open);
            var cancelled = new CancellationToken(true);

            var receiveResultSource = new TaskCompletionSource<ValueWebSocketReceiveResult>();

            mock.ReceiveAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>())
                .Returns(new ValueTask<ValueWebSocketReceiveResult>(receiveResultSource.Task));

            Action receiveTrigger = () =>
            {
                receiveResultSource.SetCanceled();
                mock.State.Returns(WebSocketState.Aborted);
            };

            var closeOutputResultSource = new TaskCompletionSource<bool>();
            mock.CloseOutputAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), cancelled)
                .Returns(closeOutputResultSource.Task);

            Action closeOutputTrigger = () =>
            {
                closeOutputResultSource.SetCanceled();
                mock.State.Returns(WebSocketState.Aborted);
                receiveTrigger();
            };

            var rxSocket = new RxWebSocket(mock);
            rxSocket.Sender.Complete();

            Should.CompleteIn(rxSocket.SendCompletion, timeout);

            var closeTask = rxSocket.CloseAsync(cancelled);

            closeOutputTrigger();
            Should.CompleteIn(closeTask, timeout);

            await mock.Received(1).CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancelled);
        }
    }
}
