using System.Collections.Generic;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Consensus
{
    public class ConsensusProposal
    {
        public UInt256[] TransactionHashes;
        public Dictionary<UInt256, Transaction> Transactions;
    }
}