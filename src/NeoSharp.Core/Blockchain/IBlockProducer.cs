using System;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain
{
    public interface IBlockProducer
    {
        uint Version { get; }
        
        Block ProduceBlock(int maxSize, DateTime generationTime, ulong nonce);
    }
}