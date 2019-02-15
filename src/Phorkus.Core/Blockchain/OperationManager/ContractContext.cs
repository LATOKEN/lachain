using Phorkus.Proto;
using Phorkus.Storage.State;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class ContractContext
    {
        public ISnapshotManager<IBlockchainSnapshot> SnapshotManager { get; }
        
        public UInt160 Sender { get; }
    }
}