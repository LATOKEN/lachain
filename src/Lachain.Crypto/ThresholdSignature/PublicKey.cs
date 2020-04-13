using System;
using Lachain.Crypto.MCL.BLS12_381;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKey : IEquatable<PublicKey>
    {
        public PublicKey(G1 rawKey)
        {
            RawKey = rawKey;
        }

        public G1 RawKey { get; }

        public byte[] ToBytes()
        {
            return G1.ToBytes(RawKey);
        }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return Mcl.Pairing(RawKey, mappedMessage).Equals(Mcl.Pairing(G1.Generator, signature.RawSignature));
        }

        public static PublicKey FromBytes(byte[] buffer)
        {
            return new PublicKey(G1.FromBytes(buffer));
        }

        public bool Equals(PublicKey other)
        {
            return other != null && RawKey.Equals(other.RawKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((PublicKey) obj);
        }

        public override int GetHashCode()
        {
            return RawKey.GetHashCode();
        }
    }
}