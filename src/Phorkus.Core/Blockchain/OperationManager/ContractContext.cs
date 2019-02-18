using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class ContractContext
    {
        public IBlockchainSnapshot Snapshot { get; set; }
        
        public UInt160 Sender { get; set; }

        public Transaction Transaction { get; set; }
    }
}