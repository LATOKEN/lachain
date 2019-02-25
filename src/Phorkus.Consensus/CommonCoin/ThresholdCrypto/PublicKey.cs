using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class PublicKey
    {
        public G1 RawKey { get; }

        public PublicKey(G1 pubKey)
        {
            RawKey = pubKey;
        }

        public bool ValidateSignature(Signature signature, byte[] message)
        {
            var mappedMessage = new G2();
            mappedMessage.SetHashOf(message);
            return Mcl.Pairing(RawKey, mappedMessage).Equals(Mcl.Pairing(G1.Generator, signature.RawSignature));
        }
    }
}