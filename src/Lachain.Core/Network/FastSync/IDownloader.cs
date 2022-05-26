using System.Collections.Generic;
using Lachain.Proto;
using Lachain.Storage.Repositories;


namespace Lachain.Core.Network.FastSync
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
        UInt256? CheckpointBlockHash { get; }
        List<(UInt256, CheckpointType)>? CheckpointStateHashes { get; }
        void DownloadCheckpoint(ulong blockNumber, string[] trieNames);
    }
}