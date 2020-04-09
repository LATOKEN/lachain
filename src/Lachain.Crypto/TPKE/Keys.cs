namespace Lachain.Crypto.TPKE
{
    public class Keys
    {
        public PublicKey PubKey { get; }
        public PrivateKey PrivateKey { get; }
        
        public Keys(PublicKey pubKey, PrivateKey privateKey)
        {
            PubKey = pubKey;
            PrivateKey = privateKey;
        }
    }
}