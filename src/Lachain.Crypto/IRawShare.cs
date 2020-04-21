using System;

namespace Lachain.Crypto
{
    public interface IRawShare : IEquatable<IRawShare>
    {
        byte[] ToBytes();
        int Id { get; }
    }
}