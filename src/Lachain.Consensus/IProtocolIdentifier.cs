using System;
using System.Collections.Generic;

namespace Lachain.Consensus
{
    public interface IProtocolIdentifier : IEquatable<IProtocolIdentifier>
    {
        long Era { get; }
        IEnumerable<byte> ToByteArray();
    }
}