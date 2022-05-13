using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    public interface IDownloader
    {
        PeerManager GetPeerManager();
        void GetTrie(UInt256 rootHash);
        void HandleBlocksFromPeer(List<Block> response, ulong requestId, ECDSAPublicKey publicKey);
        void HandleNodesFromPeer(List<TrieNodeInfo> response, ulong requestId, ECDSAPublicKey publicKey);
        void DownloadBlocks();
    }
}