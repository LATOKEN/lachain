using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public class BlockWithTransactions
    {
        public Block Block { get; }

        public IReadOnlyCollection<AcceptedTransaction> Transactions { get; }

        public BlockWithTransactions(Block block, IReadOnlyCollection<AcceptedTransaction> transactions)
        {
            Block = block;
            Transactions = transactions;
        }
    }
}