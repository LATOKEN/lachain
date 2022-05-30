using System.Collections.Generic;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IFastSynchronizerBatch
    {
        void StartSync(ulong? blockNumber, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes);
        void AddPeer(ECDSAPublicKey publicKey);
        bool IsRunning();
        bool IsCheckpointOk(ulong? blockHeight, UInt256? blockHash, List<(UInt256, CheckpointType)>? stateHashes);
    }
}