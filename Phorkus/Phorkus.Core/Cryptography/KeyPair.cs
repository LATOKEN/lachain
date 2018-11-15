namespace Phorkus.Core.Cryptography
{
    public class KeyPair
    {
        public readonly byte[] PrivateKey;
        public readonly byte[] PublicKey;
        
        public KeyPair(byte[] privateKey, byte[] publicKey)
        {
            PrivateKey = privateKey;
            PublicKey = publicKey;
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