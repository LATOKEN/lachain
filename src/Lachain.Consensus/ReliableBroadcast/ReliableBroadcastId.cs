using System;

namespace Lachain.Consensus.ReliableBroadcast
{
    public class ReliableBroadcastId : IProtocolIdentifier
    {
        protected bool Equals(ReliableBroadcastId other)
        {
            return SenderId == other.SenderId && Era == other.Era;
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
            return HashCode.Combine(SenderId, Era);
        }

        public int SenderId { get; }

        public long Era { get; }

        public ReliableBroadcastId(int validator, int era)
        {
            SenderId = validator;
            Era = (long) era;
        }

        public override string ToString()
        {
            return $"RBC (E={Era}, A={SenderId})";
        }
    }
}