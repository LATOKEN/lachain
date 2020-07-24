using System.Collections.Generic;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;

namespace Lachain.Networking
{
    public interface IPeerManager
    {
        List<PeerAddress> GetSavedPeers();

        List<PeerAddress> Start(NetworkConfig networkConfig, INetworkBroadcaster broadcaster, EcdsaKeyPair keyPair);
        void UpdatePeerTimestamp(ECDSAPublicKey publicKey);
        
        void BroadcastGetPeersRequest();
        (Peer[], ECDSAPublicKey[]) GetPeersToBroadcast();
    }
}