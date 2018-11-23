using System;
using System.Collections.Generic;
using Phorkus.Core.Network;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public interface IBlockchainSynchronizer
    {
        uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout);
        
        void HandleTransactionsFromPeer(IEnumerable<SignedTransaction> transactions, IPeer peer);
        
        void HandleBlockFromPeer(Block block, IPeer peer);
        
        void Start();
    }
}