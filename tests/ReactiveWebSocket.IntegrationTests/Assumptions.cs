using Shouldly;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;
using Xunit;

using ReactiveWebSocket;
using System.Threading.Channels;

namespace ReactiveWebSocket.IntegrationTests
{
    public class Assumptions
    {
        private static readonly TimeSpan timeout = Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(1);
        
        private readonly CancellationTokenSource cts = new CancellationTokenSource(timeout);

        private static readonly Uri bitfinexUri = new Uri("wss://api-pub.bitfinex.com/ws/2");

        [Fact]
        public async Task WebSocket_SendingInCloseSentStatus_Throws()
        {
            // Arrange
            var client = new ClientWebSocket();
            await client.ConnectAsync(bitfinexUri, this.cts.Token);
            await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, this.cts.Token);
            client.State.ShouldBe(WebSocketState.CloseSent);

            // Act
            var ex = await Should.ThrowAsync<WebSocketException>(client.SendAsync(Encoding.UTF8.GetBytes("test").AsMemory(), WebSocketMessageType.Text, true, this.cts.Token).AsTask());

            // Assert
            client.CloseStatus.ShouldBeNull();
            ex.WebSocketErrorCode.ShouldBe(WebSocketError.InvalidState);
            ex.Message.ShouldBe("The WebSocket is in an invalid state ('CloseSent') for this operation. Valid states are: 'Open, CloseReceived'");
        }

        [Fact]
        public async Task WebSocket_SendingInClosed_Throws()
        {
            // Arrange
            var client = new ClientWebSocket();
            await client.ConnectAsync(bitfinexUri, this.cts.Token);
            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, this.cts.Token);
            client.State.ShouldBe(WebSocketState.Closed);
            client.CloseStatus.ShouldBe(WebSocketCloseStatus.NormalClosure);

            // Act
            var ex = await Should.ThrowAsync<WebSocketException>(client.SendAsync(Encoding.UTF8.GetBytes("test").AsMemory(), WebSocketMessageType.Text, true, this.cts.Token).AsTask());

            // Assert
            ex.WebSocketErrorCode.ShouldBe(WebSocketError.InvalidState);
            ex.Message.ShouldBe("The WebSocket is in an invalid state ('Closed') for this operation. Valid states are: 'Open, CloseReceived'");
        }

        [Fact]
        public async Task WebSocket_SendingInAborted_Throws()
        {
            // Arrange
            var client = new ClientWebSocket();
            await client.ConnectAsync(bitfinexUri, this.cts.Token);

            var cancelled = new CancellationToken(true);
            var sending = client.SendAsync(JsonSerializer.Serialize(new PingEvent()).AsMemory(), WebSocketMessageType.Text, true, cancelled);

            _ = await Assert.ThrowsAsync<TaskCanceledException>(() => sending.AsTask());
            client.State.ShouldBe(WebSocketState.Aborted);

            // Act
            var ex = await Should.ThrowAsync<WebSocketException>(client.SendAsync(JsonSerializer.Serialize(new PingEvent()).AsMemory(), WebSocketMessageType.Text, true, default).AsTask());

            // Assert
            client.CloseStatus.ShouldBeNull();
            ex.WebSocketErrorCode.ShouldBe(WebSocketError.InvalidState);
            ex.Message.ShouldBe("The WebSocket is in an invalid state ('Aborted') for this operation. Valid states are: 'Open, CloseReceived'");
        }

        [Fact]
        public async Task WebSocket_CloseOutputAsyncCancelled_VerifyStateAborted()
        {
            var client = new ClientWebSocket();
            await client.ConnectAsync(bitfinexUri, this.cts.Token);

            Should.Throw<OperationCanceledException>(
                client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, new CancellationToken(true)),
                timeout);

            client.State.ShouldBe(WebSocketState.Aborted);
        }

        [Fact]
        public async Task WebSocket_CloseOutputAsyncCancelled_ReceiveAsyncThrows()
        {
            var client = new ClientWebSocket();
            await client.ConnectAsync(bitfinexUri, this.cts.Token);

            var channel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions() { SingleWriter = true, SingleReader = true, AllowSynchronousContinuations = false });

            var receiveLoop = channel.Writer.ReceiveLoop(client, this.cts.Token);

            await channel.Reader.WaitToReadAsync(this.cts.Token);

            var cancelled = new CancellationToken(true);

            Should.Throw<TaskCanceledException>(client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cancelled), timeout);
            Should.Throw<OperationCanceledException>(receiveLoop, timeout);
        }
    }
}
