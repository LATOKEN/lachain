using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus
{
    public class ConsensusProposal
    {
        public UInt256[] TransactionHashes;
        public Dictionary<UInt256, SignedTransaction> Transactions;
        
        public bool IsComplete => TransactionHashes.Length == Transactions.Count;
    }
}