using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class PublicKey
    {
        private readonly G1 _pubKey;

        public PublicKey(G1 pubKey)
        {
            _pubKey = pubKey;
        }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return Mcl.Pairing(_pubKey, mappedMessage).Equals(Mcl.Pairing(G1.Generator, signature.RawSignature));
        }
    }
}