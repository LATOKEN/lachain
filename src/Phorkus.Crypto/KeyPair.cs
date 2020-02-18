using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Crypto
{
    public class KeyPair : IEquatable<KeyPair>
    {
        public readonly ECDSAPrivateKey PrivateKey;
        public readonly ECDSAPublicKey PublicKey;

        public KeyPair(ECDSAPrivateKey privateKey, ECDSAPublicKey publicKey)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
        }

        public KeyPair(ECDSAPrivateKey privateKey, ICrypto crypto)
        {
            PrivateKey = privateKey;
            PublicKey = new ECDSAPublicKey
            {
                Buffer = ByteString.CopyFrom(crypto.ComputePublicKey(privateKey.Buffer.ToByteArray(), true))
            };
        }

        public bool Equals(KeyPair? other)
        {
            if (ReferenceEquals(this, other))
                return true;
            return !(other is null) && PrivateKey.Equals(other.PrivateKey);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as KeyPair);
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