using System.Collections.Generic;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Proto;


namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IDownloader
    {
        PeerManager GetPeerManager();
        void GetTrie(UInt256 rootHash);
        void HandleBlocksFromPeer(List<Block> response, ulong requestId, ECDSAPublicKey publicKey);
        void HandleNodesFromPeer(List<TrieNodeInfo> response, ulong requestId, ECDSAPublicKey publicKey);
        void HandleCheckpointBlockFromPeer(Block? block, ulong requestId, ECDSAPublicKey publicKey);
        void HandleCheckpointStateHashFromPeer(UInt256? rootHash, ulong requestId, ECDSAPublicKey publicKey);
        void DownloadBlocks();
        Block? CheckpointBlock { get; }
        List<(UInt256, CheckpointType)>? CheckpointStateHashes { get; }
        void DownloadCheckpoint(ulong blockNumber, string[] trieNames);
        void ResetCheckpointInfo();
    }
}