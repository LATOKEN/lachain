using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Consensus.HoneyBadger;

namespace Phorkus.Consensus.TPKE

{
    public class TPKESetupId : IProtocolIdentifier
    {
        public TPKESetupId(HoneyBadgerId honeyBadgerId)
        {
            Era = honeyBadgerId.Era;
        }
        
        public TPKESetupId(int era)
        {
            Era = (long) era;
        }

        public long Era { get; }
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }

        protected bool Equals(TPKESetupId other)
        {
            return Era == other.Era;
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TPKESetupId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }
    }
}