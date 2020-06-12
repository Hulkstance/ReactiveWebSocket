namespace ReactiveWebSocket
{
    public sealed class Message
    {
        public Message(MessageType type, byte[] data)
        {
            this.Type = type;
            this.Data = data;
        }

        public byte[] Data { get; }
        public MessageType Type { get; }

        public static Message Text(byte[] data) => new Message(MessageType.Text, data);
        public static Message Binary(byte[] data) => new Message(MessageType.Binary, data);
    }
}
