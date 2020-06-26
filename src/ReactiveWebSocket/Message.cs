using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

namespace ReactiveWebSocket
{
    public sealed class Message
    {
        public Message(MessageType type, byte[] data)
        {
            this.Type = type;
            this.Data = data;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Data Transfer Object")]
        public byte[] Data { get; }
        public MessageType Type { get; }

        public static Message Text(byte[] data) => new Message(MessageType.Text, data);
        public static Message Binary(byte[] data) => new Message(MessageType.Binary, data);
    }
}
