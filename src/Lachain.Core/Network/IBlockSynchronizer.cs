using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network
{
    public interface IBlockSynchronizer : IDisposable
    {
        event EventHandler<ulong> OnSignedBlockReceived;
        
        uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey);
        
        void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey);

        bool HandleBlockFromPeer(BlockInfo block, ECDSAPublicKey publicKey);
        
        ulong? GetHighestBlock();
        
        IDictionary<ECDSAPublicKey, ulong> GetConnectedPeers();

        void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers);

        void Start();
    }
}