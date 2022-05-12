using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IRequestManager
    {
        bool TryGetHashBatch(out List<UInt256> hashBatch);
        bool Done();
        bool CheckConsistency(ulong rootId);
        void HandleResponse(List<UInt256> hashBatch, List<TrieNodeInfo> response);
        void AddHash(UInt256 hash);
    }
}