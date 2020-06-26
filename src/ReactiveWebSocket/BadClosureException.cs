using System;
using System.Net.WebSockets;

namespace ReactiveWebSocket
{

    [Serializable]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Required property CloseStatus must be initialised in constructor")]
    public sealed class BadClosureException : RxWebSocketException
    {
        internal BadClosureException(WebSocketCloseStatus status, string message)
            : base(message)
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
