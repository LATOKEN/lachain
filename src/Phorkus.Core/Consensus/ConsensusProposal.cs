using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public class ConsensusProposal
    {
        public UInt256[] TransactionHashes;
        public Dictionary<UInt256, SignedTransaction> Transactions;
        public ulong Timestamp;
        public ulong Nonce;
        
        public bool IsComplete => TransactionHashes.Length == Transactions.Count;
    }
}