using System;
using Lachain.Proto;

namespace Lachain.Crypto.ECDSA
{
    public class EcdsaKeyPair : IEquatable<EcdsaKeyPair>
    {
        public readonly ECDSAPrivateKey PrivateKey;
        public readonly ECDSAPublicKey PublicKey;

        public EcdsaKeyPair(ECDSAPrivateKey privateKey, ECDSAPublicKey publicKey)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
        }

        public EcdsaKeyPair(ECDSAPrivateKey privateKey)
        {
            PrivateKey = privateKey;
            PublicKey = privateKey.GetPublicKey();
        }

        public bool Equals(EcdsaKeyPair? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            return !(other is null) && PrivateKey.Equals(other.PrivateKey);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as EcdsaKeyPair);
        }

        public override int GetHashCode()
        {
            return PublicKey.GetHashCode();
        }

        public override string ToString()
        {
            return PublicKey.ToString();
        }
    }
}