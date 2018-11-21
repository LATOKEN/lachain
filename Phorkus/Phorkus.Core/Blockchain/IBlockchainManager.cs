using Phorkus.Core.Cryptography;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        void TryBuildGenesisBlock(KeyPair keyPair);
    }
}