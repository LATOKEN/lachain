namespace Lachain.Crypto.TPKE
{
    public class Keys
    {
        public PublicKey PubKey { get; }
        public PrivateKey PrivKey { get; }
        
        public VerificationKey VerificationKey { get; }

        public Keys(PublicKey pubKey, PrivateKey privKey, VerificationKey verificationKey)
        {
            PubKey = pubKey;
            PrivKey = privKey;
            VerificationKey = verificationKey;
        }
    }
}