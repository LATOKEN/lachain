using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Phorkus.Consensus.HoneyBadger;

namespace Phorkus.Consensus.CommonSubset
{
    public class CommonSubsetId : IProtocolIdentifier
    {
        public CommonSubsetId(HoneyBadgerId honeyBadgerId)
        {
            Era = honeyBadgerId.Era;
        }

        public CommonSubsetId(int era)
        {
            Era = (long) era;
        }

        public long Era { get; }
        public IEnumerable<byte> ToByteArray()
        {
            return Encoding.ASCII.GetBytes(Era.ToString());
        }

        public override string ToString()
        {
            return $"CommonSubsetId {nameof(Era)}: {Era}";
        }

        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        protected bool Equals(CommonSubsetId other)
        {
            return Era == other.Era;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((CommonSubsetId) obj);
        }

        public override int GetHashCode()
        {
            return Era.GetHashCode();
        }

    }
}