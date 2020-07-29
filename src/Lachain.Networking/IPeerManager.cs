using System.Collections.Generic;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IPeerManager
    {
        List<PeerAddress> GetSavedPeers();

        List<PeerAddress> Start(NetworkConfig networkConfig, INetworkBroadcaster broadcaster, EcdsaKeyPair keyPair);

        void Stop();

        Peer GetCurrentPeer();
        void UpdatePeerTimestamp(ECDSAPublicKey publicKey);
        void AddPeer(Peer peer, ECDSAPublicKey publicKey);
        
        void BroadcastGetPeersRequest();
        void BroadcastPeerJoin();

        (Peer[], ECDSAPublicKey[]) GetPeersToBroadcast();
        List<PeerAddress> HandlePeersFromPeer(IEnumerable<Peer> peers, IEnumerable<ECDSAPublicKey> publicKeys);
    }
}