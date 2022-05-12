using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IRequestManager
    {
        bool TryGetHashBatch(out List<UInt256> hashBatch, out List<ulong> batchId);
        bool Done();
        bool CheckConsistency(ulong rootId);
        void HandleResponse(List<UInt256> hashBatch, List<ulong> batchId, List<TrieNodeInfo> response, ECDSAPublicKey? peer);
        void AddHash(UInt256 hash);
    }
}