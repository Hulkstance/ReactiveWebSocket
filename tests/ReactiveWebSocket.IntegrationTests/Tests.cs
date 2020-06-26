using Shouldly;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.WebSockets;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Utf8Json;
using Xunit;

namespace ReactiveWebSocket.IntegrationTests
{
    public sealed class Tests : IDisposable
    {
        private static readonly Uri bitfinexUri = new Uri("wss://api-pub.bitfinex.com/ws/2");

        private static readonly TimeSpan timeout = Debugger.IsAttached ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(1);

        private readonly CancellationTokenSource cts = new CancellationTokenSource(timeout);

        [Fact]
        public async Task RxWebSocket_ReceiveMessage()
        {
            using (var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket()))
            {
                var message = Should.CompleteIn(rxSocket.ToObservable().FirstAsync().ToTask(), timeout);
            }
        }

        [Fact]
        public async Task RxWebSocket_Ping_ShouldPong()
        {
            using (var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket()))
            {
                await rxSocket.Receiver.ReadAsync(this.cts.Token);

                var cid = "1";

                rxSocket.Sender
                    .TryWrite(Message.Text(JsonSerializer.Serialize(new PingEvent(cid))))
                    .ShouldBeTrue();


                var pongMessage = await rxSocket.Receiver.ReadAsync(this.cts.Token);

                var pong = JsonSerializer.Deserialize<PongEvent>(pongMessage.Data);
                pong.Cid.ShouldBe(cid);
            }
        }

        [Fact]
        public async Task RxWebSocket_ParallelSendPing_ShouldReceivePong()
        {
            using (var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket(), singleSender: false))
            {
                await rxSocket.Receiver.ReadAsync(this.cts.Token);

                var cidRange = Enumerable.Range(1, 10);

                Parallel.ForEach(cidRange.Select(i => i.ToString()), cid =>
                {
                    rxSocket.Sender.TryWrite(Message.Text(JsonSerializer.Serialize(new PingEvent(cid)))).ShouldBeTrue();
                });

                var pongMessages = Should.CompleteIn(rxSocket.ToObservable().Take(10).ToArray().ToTask(), timeout);
                var pongEvents = pongMessages.Select(m => JsonSerializer.Deserialize<PongEvent>(m.Data)).ToArray();
                var cids = pongEvents.Select(p => int.Parse(p.Cid)).OrderBy(cid => cid);

                cids.ShouldBe(cidRange);
            }
        }

        [Fact]
        public async Task RxWebSocket_DisposeMultipleTimes_ShouldBeOk()
        {
            // Arrange
            var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket());
            rxSocket.Dispose();

            // Act / Assert
            rxSocket.Dispose();
        }

        [Fact]
        public async Task RxWebSocket_CompleteSender_ShouldCompleteTask()
        {
            using (var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket()))
            {
                rxSocket.Sender.Complete();
                Should.CompleteIn(rxSocket.SendCompletion, timeout);
            }
        }

        [Fact]
        public async Task RxWebSocket_DisposeWithoutCompletingSender_ShouldThrow()
        {
            var rxSocket = new RxWebSocket(await this.CreateAndConnectSocket(), singleSender: false);

            rxSocket.Dispose();

            Should.Throw<TaskCanceledException>(rxSocket.SendCompletion, timeout);
        }

        private async Task<ClientWebSocket> CreateAndConnectSocket()
        {
            var socket = new ClientWebSocket();
            await socket.ConnectAsync(bitfinexUri, this.cts.Token);
            return socket;
        }

        public void Dispose()
        {
            this.cts.Dispose();
        }
    }
}
