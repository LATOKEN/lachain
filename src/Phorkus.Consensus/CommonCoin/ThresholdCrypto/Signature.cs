using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Consensus.CommonCoin.ThresholdCrypto
{
    public class Signature
    {
        private G2 _signature;

        internal Signature(G2 signature)
        {
            _signature = signature;
        }

        public G2 RawSignature => _signature;

        public bool Parity()
        {
            return false;
        }
    }
}