using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.RootProtocol
{
    public class RootProtocolId : IProtocolIdentifier
    {
        public bool Equals(IProtocolIdentifier other)
        {
            if (other == null) return false;
            if (GetType() != other.GetType()) return false;
            return Era == other.Era;
        }

        public long Era { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }

        public RootProtocolId(long era)
        {
            Era = era;
        }
    }
}