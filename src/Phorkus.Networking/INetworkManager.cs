using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public delegate void OnClientConnectedDelegate(IRemotePeer remotePeer);

    public delegate void OnClientClosedDelegate(IRemotePeer remotePeer);

    public delegate void OnClientHandshakeDelegate(Node node);

    public interface INetworkManager
    {
        event OnClientConnectedDelegate OnClientConnected;
        event OnClientClosedDelegate OnClientClosed;

        event OnClientHandshakeDelegate OnClientHandshake;

        IMessageFactory MessageFactory { get; }

        bool IsConnected(PeerAddress address);

        IRemotePeer Connect(PeerAddress address);
        IRemotePeer GetPeerByPublicKey(ECDSAPublicKey publicKey);
        bool IsReady { get; }

        void Start(NetworkConfig networkConfig, KeyPair keyPair, IMessageHandler messageHandler);

        void Stop();
    }
}