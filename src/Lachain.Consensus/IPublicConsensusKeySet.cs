using System.Collections.Generic;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Consensus
{
    public interface IPublicConsensusKeySet
    {
        int N { get; }
        int F { get; }
        PublicKey TpkePublicKey { get; }
        PublicKeySet ThresholdSignaturePublicKeySet { get; }
        IList<ECDSAPublicKey> EcdsaPublicKeySet { get; }
        public int GetValidatorIndex(ECDSAPublicKey publicKey);
        public byte[] ToBytes();
    }
}