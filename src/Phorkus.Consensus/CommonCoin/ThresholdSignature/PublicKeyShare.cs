using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdSignature
{
    public class PublicKeyShare : PublicKey
    {
        public PublicKeyShare(G1 pubKey) : base(pubKey)
        {
        }
    }
}