using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationContext
    {
        public UInt160 Sender { get; }

        public UInt256 Value => Receipt.Transaction.Value;

        public UInt256 TransactionHash => Receipt.FullHash();

        public readonly TransactionReceipt Receipt;

        public IBlockchainSnapshot Snapshot { get; }

        public InvocationContext(UInt160 sender, IBlockchainSnapshot snapshot, TransactionReceipt receipt)
        {
            Sender = sender;
            Snapshot = snapshot;
            Receipt = receipt;
        }

        public InvocationContext NextContext(UInt160 caller)
        {
            return new InvocationContext(caller, Snapshot, Receipt);
        }
    }
}