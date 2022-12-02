using System;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto
{
    public interface IRawShare : IEquatable<IRawShare>, IByteSerializable
    {
        byte[] ToBytes();
        int Id { get; }
    }
}