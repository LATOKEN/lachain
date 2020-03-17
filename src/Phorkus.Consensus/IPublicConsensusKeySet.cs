using System.Collections;
using System.Collections.Generic;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using Phorkus.Proto;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public interface IPublicConsensusKeySet
    {
        int N { get; }
        int F { get; }
        PublicKey TpkePublicKey { get; }
        VerificationKey TpkeVerificationKey { get; }
        PublicKeySet ThresholdSignaturePublicKeySet { get; }
        IList<ECDSAPublicKey> EcdsaPublicKeySet { get; }
    }
}