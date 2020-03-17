using Phorkus.Core.Blockchain.Interface;

namespace Phorkus.Core.Blockchain.Genesis
{
    public interface IGenesisBuilder
    {
        BlockWithTransactions Build();
    }
}