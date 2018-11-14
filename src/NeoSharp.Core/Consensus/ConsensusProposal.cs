using System.Collections.Generic;
using Transaction = NeoSharp.Core.Models.Transaction;

namespace NeoSharp.Core.Consensus
{
    public class ConsensusProposal
    {
        public UInt256[] TransactionHashes;
        public Dictionary<UInt256, Transaction> Transactions;
        
        public bool IsComplete => TransactionHashes.Length == Transactions.Count;
    }
}