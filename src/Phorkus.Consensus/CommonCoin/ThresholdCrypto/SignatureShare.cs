using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class SignatureShare : Signature
    {
        internal SignatureShare(G2 signature) : base(signature)
        {
        }
    }
}