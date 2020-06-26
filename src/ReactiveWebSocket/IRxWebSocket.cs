using System.Threading.Channels;
using System.Threading.Tasks;

namespace ReactiveWebSocket
{
    public interface IRxWebSocket
    {
        ChannelReader<Message> Receiver { get; }
        ChannelWriter<Message> Sender { get; }

        Task SendCompletion { get; }
    }
}
