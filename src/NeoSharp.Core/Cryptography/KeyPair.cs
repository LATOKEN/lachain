using System.Collections.Generic;
using NeoSharp.Core.Extensions;
using NeoSharp.Cryptography;
using NeoSharp.Types;

namespace NeoSharp.Core.Cryptography
{
    public class KeyPair
    {
        public readonly IEnumerable<byte> PrivateKey;
        public readonly PublicKey PublicKey;
        
        public UInt160 PublicKeyHash => PublicKey.EncodedData.ToScriptHash();
        
        public KeyPair(byte[] privateKey)
        {
            PrivateKey = privateKey;
            PublicKey = new PublicKey(Crypto.Default.ComputePublicKey(privateKey, true));
        }

        public bool Equals(KeyPair other)
        {
            if (ReferenceEquals(this, other))
                return true;
            return !(other is null) && PublicKey.Equals(other.PublicKey);
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