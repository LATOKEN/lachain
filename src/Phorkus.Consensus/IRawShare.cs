using System;
using System.Collections.Generic;

namespace Phorkus.Consensus
{
    public interface IRawShare : IEquatable<IRawShare>
    {
        byte[] ToBytes();
        int Id { get; }
    }
}