using System.Runtime.Serialization;

namespace ReactiveWebSocket.IntegrationTests
{
    public class PingEvent
    {
        public PingEvent(string cid = default)
        {
            this.Event = "ping";
            this.Cid = cid;
        }

        [DataMember(Name = "event")]
        public string Event { get; }

        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "cid", Order = 2)]
        public string Cid { get; }
    }
}
