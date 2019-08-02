using System.Collections.Generic;

namespace Phorkus.Consensus
{
    public interface IProtocolIdentifier
    {
        ulong Era { get; }
        IEnumerable<byte> ToByteArray();
    }
}