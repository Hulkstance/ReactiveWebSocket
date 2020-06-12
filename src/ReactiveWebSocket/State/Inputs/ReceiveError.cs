using System;

namespace ReactiveWebSocket.State.Inputs
{
    internal sealed class ReceiveError : Input
    {
        public ReceiveError(Exception error)
        {
            this.Error = error;
        }

        public Exception Error { get; }
    }
}
