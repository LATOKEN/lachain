using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(long era, long associatedValidatorId)
        {
            Era = era;
            AssociatedValidatorId = associatedValidatorId;
        }

        public long Era { get; }
        public long AssociatedValidatorId { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era).Concat(BitConverter.GetBytes(AssociatedValidatorId));
        }

        protected bool Equals(BinaryAgreementId other)
        {
            return Era == other.Era && AssociatedValidatorId == other.AssociatedValidatorId;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((BinaryAgreementId) obj);
        }
        
        public bool Equals(IProtocolIdentifier other)
        {
            return Equals((object) other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Era.GetHashCode() * 397) ^ AssociatedValidatorId.GetHashCode();
            }
        }
        
        public override string ToString()
        {
            return $"BA (E={Era}, A={AssociatedValidatorId})";
        }
    }
}