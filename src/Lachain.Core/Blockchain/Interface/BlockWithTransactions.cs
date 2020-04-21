using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public class BlockWithTransactions
    {
        public Block Block { get; }

        public ICollection<TransactionReceipt> Transactions { get; }

        public BlockWithTransactions(Block block, ICollection<TransactionReceipt> transactions)
        {
            Block = block;
            Transactions = transactions;
        }
    }
}