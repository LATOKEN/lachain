using Phorkus.Crypto;

namespace Phorkus.Networking
{
    public delegate void OnClientConnectedDelegate(IRemotePeer remotePeer);
    public delegate void OnClientClosedDelegate(IRemotePeer remotePeer);
    
    public interface INetworkManager
    {
        event OnClientConnectedDelegate OnClientConnected;
        event OnClientClosedDelegate OnClientClosed;

        IMessageFactory MessageFactory { get; }
        
        bool IsConnected(PeerAddress address);

        IRemotePeer Connect(PeerAddress address);

        bool IsReady { get; }

        void Start(NetworkConfig networkConfig, KeyPair keyPair, IMessageHandler messageHandler);

        void Stop();
    }
}