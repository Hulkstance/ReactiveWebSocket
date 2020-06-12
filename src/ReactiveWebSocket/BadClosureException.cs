using System;
using System.Net.WebSockets;

namespace ReactiveWebSocket
{

    [Serializable]
    public class BadClosureException : RxWebSocketException
    {
        public BadClosureException(WebSocketCloseStatus status, string? description) : base(description ?? string.Empty)
        {
            this.CloseStatus = status;
        }

        public BadClosureException(WebSocketCloseStatus status, string? description, Exception inner) : base(description ?? string.Empty, inner)
        {
            this.CloseStatus = status;
        }

        protected BadClosureException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.CloseStatus = (WebSocketCloseStatus)info.GetValue(nameof(CloseStatus), typeof(WebSocketCloseStatus));
        }

        public WebSocketCloseStatus CloseStatus { get; }
    }
}
