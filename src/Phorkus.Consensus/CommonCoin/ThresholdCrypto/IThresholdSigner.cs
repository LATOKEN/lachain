using System;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public interface IThresholdSigner
    {
        SignatureShare Sign();
        bool AddShare(PublicKeyShare pubKey, SignatureShare sigShare, out Signature signature);
    }
}