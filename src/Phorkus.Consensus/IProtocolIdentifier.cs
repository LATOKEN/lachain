using System.Collections.Generic;

namespace Phorkus.Consensus
{
    public interface IProtocolIdentifier
    {
        uint Era { get; }
        IEnumerable<byte> ToByteArray();
    }
}