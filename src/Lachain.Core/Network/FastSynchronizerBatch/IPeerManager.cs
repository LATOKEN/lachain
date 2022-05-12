using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IPeerManager
    {
        void AddPeer(ECDSAPublicKey publicKey, ulong height);
        bool TryGetPeer(out Peer? peer);
        bool TryFreePeer(Peer peer, bool success = true);
        int GetTotalPeerCount();
        ulong? GetHeightForPeer(Peer peer);
    }
}