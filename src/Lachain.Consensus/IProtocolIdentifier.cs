using System;

namespace Lachain.Consensus
{
    public interface IProtocolIdentifier : IEquatable<IProtocolIdentifier>
    {
        long Era { get; }
    }
}