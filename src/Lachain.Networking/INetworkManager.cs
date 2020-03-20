using System.Collections;
using System.Collections.Generic;
using Lachain.Crypto;
using Lachain.Proto;

namespace Lachain.Networking
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

        void Start(NetworkConfig networkConfig, ECDSAKeyPair keyPair, IMessageHandler messageHandler);

        void WaitForHandshake(IEnumerable<ECDSAPublicKey> peerKeys);

        void Stop();
    }
}