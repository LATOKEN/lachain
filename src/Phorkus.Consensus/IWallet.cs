using System;
using System.Collections;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;

namespace Phorkus.Consensus
{
    public interface IWallet
    {
        int N { get; }
        int F { get; }
        
        PublicKeySet PublicKeySet { get; set; }
        
        PrivateKeyShare PrivateKeyShare { get; set; }
    }
}