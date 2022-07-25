using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network
{
    public interface IBlockSynchronizer : IDisposable
    {
        event EventHandler<ulong> OnSignedBlockReceived;
        
        uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout, out List<TransactionReceipt> receipts);

        uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey);
        
        void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey);

        bool HandleBlockFromPeer(BlockInfo block, ECDSAPublicKey publicKey);
        
        ulong? GetHighestBlock();
        
        IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers();

        void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers);

        void Start(bool startFastSync);
        void HandleCheckpointFromPeer(List<CheckpointInfo> checkpoints, ECDSAPublicKey publicKey, ulong requestId);
    }
}