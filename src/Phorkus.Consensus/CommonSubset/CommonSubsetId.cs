using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class CommonSubsetId : IProtocolIdentifier
    {
        public bool Equals(IProtocolIdentifier other)
        {
            throw new NotImplementedException();
        }

        public ulong Era { get; }
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }
    }
}