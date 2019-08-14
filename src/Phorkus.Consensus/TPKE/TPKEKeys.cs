namespace Phorkus.Consensus.TPKE
{
    public class TPKEKeys
    {
        public TPKEPubKey PubKey { get; }
        public TPKEPrivKey PrivKey { get; }

        public TPKEKeys(TPKEPubKey pubKey, TPKEPrivKey privKey)
        {
            PubKey = pubKey;
            PrivKey = privKey;
        }
    }
}