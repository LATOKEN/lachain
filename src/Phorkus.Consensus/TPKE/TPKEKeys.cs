namespace Phorkus.Consensus.TPKE
{
    public class TPKEKeys
    {
        public TPKEPubKey PubKey { get; }
        public TPKEPrivKey PrivKey { get; }
        
        public TPKEVerificationKey VerificationKey { get;  }

        public TPKEKeys(TPKEPubKey pubKey, TPKEPrivKey privKey, TPKEVerificationKey verificationKey)
        {
            PubKey = pubKey;
            PrivKey = privKey;
            VerificationKey = verificationKey;
        }
    }
}