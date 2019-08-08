using System;
using System.Collections.Generic;

namespace Phorkus.Consensus
{
    public interface IProtocolIdentifier : IEquatable<IProtocolIdentifier>
    {
        ulong Era { get; }
        IEnumerable<byte> ToByteArray();
    }
}