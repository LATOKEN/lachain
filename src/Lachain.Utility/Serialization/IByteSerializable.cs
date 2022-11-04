using System;

namespace Lachain.Utility.Serialization
{
    public interface IByteSerializable
    {
        // When implementing this interface make sure to implement also following methods:
        // public static MyClass FromBytes(byte[] bytes);
        public byte[] ToBytes();
    }
}