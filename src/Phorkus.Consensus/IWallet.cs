using System;
using System.Collections;
using System.Collections.Generic;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.TPKE;

namespace Phorkus.Consensus
{
    public interface IWallet
    {
        int N { get; }
        int F { get; }
        
        TPKEPubKey TpkePubKey { get; set; }
        
        TPKEPrivKey TpkePrivKey { get; set; }

        TPKEVerificationKey TpkeVerificationKey { get; set; }
        
        PublicKeySet PublicKeySet { get; set; }
        
        PrivateKeyShare PrivateKeyShare { get; set; }
        
        ISet<IProtocolIdentifier> ProtocolIds { get; }
    }
}