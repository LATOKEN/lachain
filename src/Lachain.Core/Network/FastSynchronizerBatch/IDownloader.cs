using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.Repositories;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IDownloader
    {
        PeerManager GetPeerManager();
        void GetTrie(UInt256 rootHash);
        void HandleBlocksFromPeer(List<Block> response, ulong requestId, ECDSAPublicKey publicKey);
        void HandleNodesFromPeer(List<TrieNodeInfo> response, ulong requestId, ECDSAPublicKey publicKey);
        void DownloadBlocks();
        ulong? CheckpointBlockHash { get; }
        List<(UInt256, CheckpointType)>? CheckpointStateHashes { get; }
        void IsCheckpointOk(ulong blockNumber, string[] trieNames);
    }
}