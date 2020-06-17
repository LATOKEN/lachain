using System;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;

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
            return GT.Pairing(RawKey, mappedMessage).Equals(GT.Pairing(G1.Generator, signature.RawSignature));
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
            return G1.ByteSize;
        }

        public void Serialize(Memory<byte> bytes)
        {
            RawKey.ToBytes().CopyTo(bytes);
        }

        public static PublicKey FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return new PublicKey(G1.FromBytes(bytes.ToArray()));
        }
    }
}