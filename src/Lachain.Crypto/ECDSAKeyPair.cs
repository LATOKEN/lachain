using System;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Crypto
{
    public class ECDSAKeyPair : IEquatable<ECDSAKeyPair>
    {
        public readonly ECDSAPrivateKey PrivateKey;
        public readonly ECDSAPublicKey PublicKey;

        public ECDSAKeyPair(ECDSAPrivateKey privateKey, ECDSAPublicKey publicKey)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
        }

        public ECDSAKeyPair(ECDSAPrivateKey privateKey, ICrypto crypto)
        {
            PrivateKey = privateKey;
            PublicKey = new ECDSAPublicKey
            {
                Buffer = ByteString.CopyFrom(crypto.ComputePublicKey(privateKey.Buffer.ToByteArray(), true))
            };
        }

        public bool Equals(ECDSAKeyPair? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            return !(other is null) && PrivateKey.Equals(other.PrivateKey);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as ECDSAKeyPair);
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