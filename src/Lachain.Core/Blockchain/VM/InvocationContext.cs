using Lachain.Core.Blockchain.Hardfork;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;

namespace Lachain.Core.Blockchain.VM
{
    public class InvocationContext
    {
        public readonly UInt160 Sender;

        public UInt256 Value => Receipt.Transaction.Value;

        public UInt256 TransactionHash => Receipt.FullHash(HardforkHeights.IsHardfork_9Active(Snapshot.Blocks.GetTotalBlockHeight() + 1));

        public readonly TransactionReceipt Receipt;

        public IBlockchainSnapshot Snapshot { get; }
        
        public InvocationMessage? Message { get; set; }

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