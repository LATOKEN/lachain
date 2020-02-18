using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Networking
{
    public delegate void ClientConnectedDelegate(IRemotePeer remotePeer);

    public delegate void ClientClosedDelegate(IRemotePeer remotePeer);

    public delegate void ClientHandshakeDelegate(Node node);

    public interface INetworkManager
    {
        event ClientConnectedDelegate OnClientConnected;
        event ClientClosedDelegate OnClientClosed;

        event ClientHandshakeDelegate OnClientHandshake;

        IMessageFactory? MessageFactory { get; }

        bool IsConnected(PeerAddress address);

        IRemotePeer? Connect(PeerAddress address);
        IRemotePeer? GetPeerByPublicKey(ECDSAPublicKey publicKey);
        bool IsReady { get; }

        void Start(NetworkConfig networkConfig, KeyPair keyPair, IMessageHandler messageHandler);

        void Stop();
    }
}