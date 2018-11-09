using NeoSharp.Core.Storage.Blockchain;
using NeoSharp.Core.Storage.State;

namespace NeoSharp.Core.Storage
{
    public interface IRepository :
        IBlockchainRepository,
        IStateRepository
    {
    }
}