using System.Collections;
using System.Collections.Generic;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
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
    }
}