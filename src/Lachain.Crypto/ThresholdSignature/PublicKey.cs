using System;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;

namespace Lachain.Crypto.ThresholdSignature
{
    public class PublicKey : IEquatable<PublicKey>, IFixedWidth
    {
        public PublicKey(G1 rawKey)
        {
            RawKey = rawKey;
        }

        public G1 RawKey { get; }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return Mcl.Pairing(RawKey, mappedMessage).Equals(Mcl.Pairing(G1.Generator, signature.RawSignature));
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

        public static int Width()
        {
            return G1.Width();
        }

        public void Serialize(Memory<byte> bytes)
        {
            RawKey.Serialize(bytes);
        }

        public static PublicKey FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return new PublicKey(G1.FromBytes(bytes));
        }
    }
}