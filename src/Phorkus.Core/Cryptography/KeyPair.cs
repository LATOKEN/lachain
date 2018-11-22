using System;
using Phorkus.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Cryptography
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
            PublicKey = crypto.ComputePublicKey(privateKey.Buffer.ToByteArray(), true).ToPublicKey();
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