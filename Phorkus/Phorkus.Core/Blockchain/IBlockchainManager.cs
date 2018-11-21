using Phorkus.Core.Cryptography;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock(KeyPair keyPair);
    }
}