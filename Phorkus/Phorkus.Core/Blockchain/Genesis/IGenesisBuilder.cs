using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Genesis
{
    public interface IGenesisBuilder
    {
        Block Build();
    }
}