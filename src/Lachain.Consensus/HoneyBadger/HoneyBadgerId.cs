namespace Lachain.Consensus.HoneyBadger

{
    public class HoneyBadgerId : IProtocolIdentifier
    {
        public HoneyBadgerId(long era)
        {
            Era = era;
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

        public override string ToString()
        {
            return $"HB (Er={Era})";
        }
    }
}