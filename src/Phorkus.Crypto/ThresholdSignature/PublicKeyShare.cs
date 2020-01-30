using System;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Crypto.ThresholdSignature
{
    public class PublicKeyShare : PublicKey, IEquatable<PublicKeyShare>
    {
        public PublicKeyShare(G1 pubKey) : base(pubKey)
        {
        }
        
        public new byte[] ToByteArray()
        {
            return G1.ToBytes(RawKey);
        }

        public new static PublicKeyShare FromBytes(byte[] buffer)
        {
            return new PublicKeyShare(G1.FromBytes(buffer));
        }

        public bool Equals(PublicKeyShare other)
        {
            return other != null && RawKey.Equals(other.RawKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PublicKeyShare) obj);
        }

        public override int GetHashCode()
        {
            return RawKey.GetHashCode();
        }
    }
}