using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public class BlockWithTransactions
    {
        public Block Block { get; }

        public IReadOnlyCollection<TransactionReceipt> Transactions { get; }

        public BlockWithTransactions(Block block, IReadOnlyCollection<TransactionReceipt> transactions)
        {
            Block = block;
            Transactions = transactions;
        }
    }
}