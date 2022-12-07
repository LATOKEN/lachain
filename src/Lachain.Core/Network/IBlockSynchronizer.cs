using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network
{
    public interface IBlockSynchronizer : IDisposable
    {
        event EventHandler<ulong> OnSignedBlockReceived;
        
        void TxReceivedFromPeer(SyncPoolReply reply, ECDSAPublicKey peer);
        
        void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey);

        void BlockReceivedFromPeer(SyncBlocksReply reply, ECDSAPublicKey peer);
        
        ulong? GetHighestBlock();
        
        IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers();

        void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers);

        void Start();
    }
}