using Google.Protobuf;

namespace Phorkus.Networking
{
    public interface IBroadcaster
    {
        void Broadcast<T>(IMessage<T> message)
            where T : IMessage<T>;
    }
}