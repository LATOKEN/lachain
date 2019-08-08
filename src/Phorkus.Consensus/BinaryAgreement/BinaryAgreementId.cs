using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Consensus.BinaryAgreement
{
    public class BinaryAgreementId : IProtocolIdentifier
    {
        public BinaryAgreementId(ulong era, ulong agreement)
        {
            Era = era;
            Agreement = agreement;
        }

        public ulong Era { get; }
        public ulong Agreement { get; }
        
        public IEnumerable<byte> ToByteArray()
        {
            return BitConverter.GetBytes(Era).Concat(BitConverter.GetBytes(Agreement));
        }

        protected bool Equals(BinaryAgreementId other)
        {
            return Era == other.Era && Agreement == other.Agreement;
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
                return (Era.GetHashCode() * 397) ^ Agreement.GetHashCode();
            }
        }
    }
}