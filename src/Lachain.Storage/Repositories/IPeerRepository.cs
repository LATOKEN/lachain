using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public interface IPeerRepository
    {
        ICollection<ECDSAPublicKey> GetPeerList();
        bool RemovePeer(ECDSAPublicKey publicKey);
        bool AddOrUpdatePeer(ECDSAPublicKey publicKey, Peer peer);
        
        bool UpdatePeerTimestampIfExist(ECDSAPublicKey publicKey);
        bool ContainsPublicKey(ECDSAPublicKey publicKey);
        Peer? GetPeerByPublicKey(ECDSAPublicKey publicKey);
        int GetPeersCount();
    }
}