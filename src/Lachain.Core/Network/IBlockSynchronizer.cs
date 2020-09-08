using System;
using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Network
{
    public interface IBlockSynchronizer : IDisposable
    {
        uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout);

        uint HandleTransactionsFromPeer(IEnumerable<TransactionReceipt> transactions, ECDSAPublicKey publicKey);

        bool HandleBlockFromPeer(Block block, ECDSAPublicKey publicKey);

        void HandlePeerHasBlocks(ulong blockHeight, ECDSAPublicKey publicKey);

        bool IsSynchronizingWith(IEnumerable<ECDSAPublicKey> peers);

        ulong? GetHighestBlock();

        void SynchronizeWith(IEnumerable<ECDSAPublicKey> peers);

        void Start();
    }
}