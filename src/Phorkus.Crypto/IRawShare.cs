using System;

namespace Phorkus.Consensus
{
    public interface IRawShare : IEquatable<IRawShare>
    {
        byte[] ToBytes();
        int Id { get; }
    }
}