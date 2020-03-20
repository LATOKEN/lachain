using Lachain.Proto;
using Lachain.Storage.State;

namespace Lachain.Core.Blockchain.OperationManager
{
    public class ContractContext
    {
        public IBlockchainSnapshot? Snapshot { get; set; }
        
        public UInt160? Sender { get; set; }

        public Transaction? Transaction { get; set; }
    }
}