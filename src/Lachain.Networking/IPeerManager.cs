using System;
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

        ECDSAPublicKey[] GetStakers();
        PeerAddress? GetPeerAddressByPublicKey(ECDSAPublicKey key);
        Peer? GetPeerByPublicKey(ECDSAPublicKey key);
        List<PeerAddress> GetPeerAddressesByPublicKeys(IEnumerable<ECDSAPublicKey> keys);
        void UpdatePeerTimestamp(ECDSAPublicKey publicKey);
        void AddPeer(Peer peer, ECDSAPublicKey publicKey);
        
        void BroadcastGetPeersRequest();
        void BroadcastPeerJoinRequest();
        string GetExternalIp();

        (Peer[], ECDSAPublicKey[]) GetPeersToBroadcast();
        List<PeerAddress> HandlePeersFromPeer(IEnumerable<Peer> peers, IEnumerable<ECDSAPublicKey> publicKeys);
        string CheckLocalConnection(string host);
    }
}