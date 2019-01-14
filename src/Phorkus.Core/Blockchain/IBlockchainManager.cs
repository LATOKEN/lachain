using System.Collections.Generic;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock();

        void PersistBlockManually(Block block, IEnumerable<SignedTransaction> transactions);
    }
}