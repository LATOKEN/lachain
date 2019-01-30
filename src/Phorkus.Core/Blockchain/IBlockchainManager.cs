using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock();

        void PersistBlockManually(Block block, IEnumerable<AcceptedTransaction> transactions);
    }
}