using System.Collections;
using System.Collections.Generic;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using Phorkus.Proto;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public interface IWallet
    {
        int N { get; }
        int F { get; }

        PublicKey TpkePublicKey { get; }

        PrivateKey TpkePrivateKey { get; }

        VerificationKey TpkeVerificationKey { get; }

        PublicKeySet ThresholdSignaturePublicKeySet { get; }

        PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; }

        ECDSAPublicKey EcdsaPublicKey { get; }
        
        IEnumerable<ECDSAPublicKey> EcdsaPublicKeySet { get; }

        ECDSAPrivateKey EcdsaPrivateKey { get; }

        ISet<IProtocolIdentifier> ProtocolIds { get; } // TODO: delete this
    }
}