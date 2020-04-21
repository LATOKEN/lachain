using Lachain.Core.Blockchain.Interface;

namespace Lachain.Core.Blockchain.Genesis
{
    public interface IGenesisBuilder
    {
        BlockWithTransactions Build();
    }
}