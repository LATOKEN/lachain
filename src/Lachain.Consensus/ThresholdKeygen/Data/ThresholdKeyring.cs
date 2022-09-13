using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Serialization;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public struct ThresholdKeyring
    {
        public Crypto.TPKE.PrivateKey TpkePrivateKey;
        public Crypto.TPKE.PublicKey TpkePublicKey;
        public List<Crypto.TPKE.PublicKey> TpkeVerificationPublicKeys;
        public Crypto.ThresholdSignature.PublicKeySet ThresholdSignaturePublicKeySet;
        public Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKey;

        public UInt256 PublicPartHash()
        {
            return TpkePublicKey.ToBytes().Concat(ThresholdSignaturePublicKeySet.ToBytes()).Keccak();
        }
    }
}