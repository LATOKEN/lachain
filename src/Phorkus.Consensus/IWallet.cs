using System.Collections.Generic;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Consensus
{
    public interface IWallet
    {
        int N { get; }
        int F { get; }
        
        PublicKey TpkePublicKey { get; set; }
        
        PrivateKey TpkePrivateKey { get; set; }

        VerificationKey TpkeVerificationKey { get; set; }
        
        PublicKeySet ThresholdSignaturePublicKeySet { get; set; }
        
        PrivateKeyShare ThresholdSignaturePrivateKeyShare { get; set; }
        
        ISet<IProtocolIdentifier> ProtocolIds { get; } // TODO: delete this
    }
}