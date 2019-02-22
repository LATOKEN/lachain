using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class PublicKeyShare : PublicKey
    {
        public PublicKeyShare(G1 pubKey) : base(pubKey)
        {
        }
    }
}