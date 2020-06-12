using System;

namespace ReactiveWebSocket.State.Inputs
{
    internal sealed class SendError : Input
    {
        public SendError(Exception error)
        {
            this.Error = error;
        }

        public Exception Error { get; }
    }
}
