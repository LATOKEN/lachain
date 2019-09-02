using System;
using System.Collections.Generic;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class ReliableBroadcastId: IProtocolIdentifier
    {
        protected bool Equals(ReliableBroadcastId other)
        {
            return AssociatedValidatorId == other.AssociatedValidatorId && Era == other.Era;
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
            return Equals((ReliableBroadcastId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (AssociatedValidatorId.GetHashCode() * 397) ^ Era.GetHashCode();
            }
        }

        public int AssociatedValidatorId { get; }
        
        public long Era { get; }

        public ReliableBroadcastId(int validator, int era)
        {
            AssociatedValidatorId = validator;
            Era = (long) era;
        }
        public IEnumerable<byte> ToByteArray()
        {
            throw new NotImplementedException();
        }
    }
}