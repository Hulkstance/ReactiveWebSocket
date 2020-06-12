using System.Threading;

namespace ReactiveWebSocket.State.Inputs
{
    internal sealed class CloseAsync : Input
    {
        public CloseAsync(CancellationToken cancellationToken)
        {
            this.Token = cancellationToken;
        }

        public CancellationToken Token { get; }
    }
}
