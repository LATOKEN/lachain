using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Serialization;

namespace Lachain.Consensus.ThresholdKeygen.Data
{
    public struct ThresholdKeyring
    {
        public Crypto.ThresholdSignature.PublicKeySet ThresholdSignaturePublicKeySet;
        public Crypto.ThresholdSignature.PrivateKeyShare ThresholdSignaturePrivateKey;

        public UInt256 PublicPartHash()
        {
            return ThresholdSignaturePublicKeySet.ToBytes().Keccak();
        }
    }
}