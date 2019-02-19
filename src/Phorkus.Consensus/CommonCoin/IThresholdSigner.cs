using System;

namespace Phorkus.Consensus.CommonCoin
{
    public interface IThresholdSigner
    {
        void Sign();
        event EventHandler<byte[]> SignatureProduced;
    }
}