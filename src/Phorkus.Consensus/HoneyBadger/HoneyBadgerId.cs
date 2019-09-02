using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.HoneyBadger

{
    public class HoneyBadgerId : IProtocolIdentifier
    {
        public HoneyBadgerId(int era)
        {
            Era = (long) era;
        }
        
        protected bool Equals(HoneyBadgerId other)
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
            return Equals((HoneyBadgerId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }

        public long Era { get; }
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }
    }
}