using System.Collections.Generic;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain
{
    public interface IBlockPool : IEnumerable<Block>
    {
        int Capacity { get; }
        
        int Size { get; }

        bool TryGet(uint height, out Block block);

        bool TryAdd(Block block);

        bool TryRemove(uint height);
    }
}