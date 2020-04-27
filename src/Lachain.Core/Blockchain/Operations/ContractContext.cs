using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.Operations
{
    public class ContractContext
    {
        public IBlockchainSnapshot? Snapshot { get; set; }
        
        public UInt160? Sender { get; set; }

        public TransactionReceipt? Receipt { get; set; }

        public ulong GasRemaining => Receipt.Transaction.GasLimit - Receipt.GasUsed;
    }
}