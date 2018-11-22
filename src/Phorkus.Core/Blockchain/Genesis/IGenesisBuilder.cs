using Phorkus.Core.Cryptography;

namespace Phorkus.Core.Blockchain.Genesis
{
    public interface IGenesisBuilder
    {
        BlockWithTransactions Build();
    }
}