using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.HoneyBadger

{
    public class HoneyBadgerId : IProtocolIdentifier
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