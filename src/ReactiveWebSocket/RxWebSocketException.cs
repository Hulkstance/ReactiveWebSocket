using System;

namespace ReactiveWebSocket
{

    [Serializable]
    public class RxWebSocketException : Exception
    {
        public RxWebSocketException() { }
        public RxWebSocketException(string message) : base(message) { }
        public RxWebSocketException(string message, Exception inner) : base(message, inner) { }
        protected RxWebSocketException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
