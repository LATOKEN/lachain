using System;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Crypto
{
    public class KeyPair : IEquatable<KeyPair>
    {
        public readonly PrivateKey PrivateKey;
        public readonly PublicKey PublicKey;

        public KeyPair(PrivateKey privateKey, PublicKey publicKey)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
        }

        public KeyPair(PrivateKey privateKey, ICrypto crypto)
        {
            PrivateKey = privateKey;
            PublicKey = new PublicKey
            {
                Buffer = ByteString.CopyFrom(crypto.ComputePublicKey(privateKey.Buffer.ToByteArray(), true))
            };
        }

        public bool Equals(KeyPair other)
        {
            if (ReferenceEquals(this, other))
                return true;
            return !(other is null) && PrivateKey.Equals(other.PrivateKey);
        }

        public override bool Equals(object obj)
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