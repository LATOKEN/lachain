namespace Phorkus.Core.Network
{
    public interface INetworkManager : IBroadcaster
    {
        void Start();

        void Stop();
    }
}