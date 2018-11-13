using System;
using NeoSharp.Core.Models;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain
{
    public interface IBlockProducer
    {
        uint Version { get; }
        
        Block ProduceBlock(int maxSize, DateTime generationTime, ulong nonce, UInt160 producerAddress);
    }
}