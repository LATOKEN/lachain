namespace Phorkus.Networking
{
    public delegate void OnClientConnectedDelegate(IRemotePeer remotePeer);
    public delegate void OnClientClosedDelegate(IRemotePeer remotePeer);
    
    public interface INetworkManager
    {
        event OnClientConnectedDelegate OnClientConnected;
        event OnClientClosedDelegate OnClientClosed;

        bool IsConnected(PeerAddress address);

        IRemotePeer Connect(PeerAddress address);

        void Start();

        void Stop();
    }
}