using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IFastSynchronizerBatch
    {
        void StartSync(ulong blockNumber, UInt256 blockHash, List<(UInt256, CheckpointType)> stateHashes);
        void AddPeer(ECDSAPublicKey publicKey);
    }
}