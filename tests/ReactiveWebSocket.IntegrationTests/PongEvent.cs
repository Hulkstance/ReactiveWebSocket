using System.Runtime.Serialization;

namespace ReactiveWebSocket.IntegrationTests
{
    public class PongEvent
    {
        [DataMember(Name = "event")]
        public string Event { get; set; }
        [DataMember(EmitDefaultValue = false, IsRequired = false, Name = "cid")]
        public string Cid { get; set; }
    }
}
