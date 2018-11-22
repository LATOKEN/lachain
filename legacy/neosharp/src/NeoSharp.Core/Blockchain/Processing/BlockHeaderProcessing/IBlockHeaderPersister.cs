using System.Collections.Generic;
using System.Threading.Tasks;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing.BlockHeaderProcessing
{
    public interface IBlockHeaderPersister
    {
        Task<IEnumerable<BlockHeader>> Persist(params BlockHeader[] blockHeaders);

        Task Update(BlockHeader blockHeader);
    }
}