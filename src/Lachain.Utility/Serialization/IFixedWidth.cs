using System;

namespace Lachain.Utility.Serialization
{
    public interface IFixedWidth
    {
        // When implementing this interface make sure to implement also following methods:
        
        // public static int Width();
        // public static MyClass FromBytes(ReadOnlyMemory<byte> bytes);
        
        public void Serialize(Memory<byte> bytes);
    }
}