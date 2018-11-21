using System.Collections.Generic;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock(KeyPair keyPair);

        void PersistBlockManually(Block block, IEnumerable<SignedTransaction> transactions);
    }
}