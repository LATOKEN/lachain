using Lachain.Consensus.HoneyBadger;

namespace Lachain.Consensus.CommonSubset
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

        public override string ToString()
        {
            return $"ACS ({nameof(Era)}={Era})";
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