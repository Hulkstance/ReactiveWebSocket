using System;
using System.Net.WebSockets;

namespace ReactiveWebSocket
{

    [Serializable]
    public sealed class BadClosureException : RxWebSocketException
    {
        internal BadClosureException(WebSocketCloseStatus status, string? description) : base(description ?? string.Empty)
        {
            this.CloseStatus = status;
        }

        internal BadClosureException(WebSocketCloseStatus status, string? description, Exception inner) : base(description ?? string.Empty, inner)
        {
            this.CloseStatus = status;
        }

        private BadClosureException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.CloseStatus = (WebSocketCloseStatus)info.GetValue(nameof(CloseStatus), typeof(WebSocketCloseStatus));
        }

        public WebSocketCloseStatus CloseStatus { get; }
    }
}
