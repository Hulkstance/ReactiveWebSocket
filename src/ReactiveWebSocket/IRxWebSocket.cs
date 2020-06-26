using System.Threading.Channels;
using System.Threading.Tasks;

namespace ReactiveWebSocket
{
    public interface IRxWebSocket
    {
        /// <summary>
        /// ChannelReader for received messages. 
        /// </summary>
        ChannelReader<Message> Receiver { get; }
        ChannelWriter<Message> Sender { get; }

        Task SendCompletion { get; }
    }
}
