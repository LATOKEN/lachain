using System;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public interface IThresholdSigner
    {
        SignatureShare Sign();
        void AddShare(PublicKeyShare pubKey, SignatureShare sigShare);
        event EventHandler<Signature> SignatureProduced;
    }
}