using System.Collections.Generic;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain
{
    public class BlockWithTransactions
    {
        public Block Block { get; }

        public IReadOnlyCollection<SignedTransaction> Transactions;

        public BlockWithTransactions(Block block, IReadOnlyCollection<SignedTransaction> transactions)
        {
            Block = block;
            Transactions = transactions;
        }
    }
}