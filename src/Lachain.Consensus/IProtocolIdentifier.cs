using System;
using Lachain.Utility.Serialization;

namespace Lachain.Consensus
{
    public interface IProtocolIdentifier : IEquatable<IProtocolIdentifier>, IByteSerializable
    {
        long Era { get; }
    }
}