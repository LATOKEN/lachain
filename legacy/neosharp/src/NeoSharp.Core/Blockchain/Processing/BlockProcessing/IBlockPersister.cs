using System.Threading.Tasks;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing.BlockProcessing
{
    public interface IBlockPersister
    {
        Task Persist(params Block[] blocks);
    }
}
