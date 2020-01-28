using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Crypto.TPKE
{
    public class EncryptedShare
    {
        protected bool Equals(EncryptedShare other)
        {
            return U.Equals(other.U) && V.SequenceEqual(other.V) && W.Equals(other.W) && Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((EncryptedShare) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = U.GetHashCode();
                hashCode = (hashCode * 397) ^ (V != null ? V.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ W.GetHashCode();
                hashCode = (hashCode * 397) ^ Id;
                return hashCode;
            }
        }

        public G1 U { get; }
        public byte[] V { get; }
        public G2 W { get; }
        public int Id { get; }

        public EncryptedShare(G1 _U, byte[] _V, G2 _W, int id)
        {
            U = _U;
            V = _V;
            W = _W;
            Id = id;
        }
        
    }
}