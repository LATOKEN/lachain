using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Consensus
{
    public class PublicConsensusKeySet : IPublicConsensusKeySet
    {
        public int N { get; }
        public int F { get; }
        public PublicKey TpkePublicKey { get; }
        public PublicKeySet ThresholdSignaturePublicKeySet { get; }
        private readonly List<ECDSAPublicKey> _ecdsaPublicKeys;
        public IList<ECDSAPublicKey> EcdsaPublicKeySet => _ecdsaPublicKeys;

        private List<PublicKey> _tpkeVerificationKeys;

        public PublicConsensusKeySet(int n, int f,
            PublicKey tpkePublicKey,
            IEnumerable<PublicKey> tpkeVerificationKeys, 
            PublicKeySet thresholdSignaturePublicKeySet,
            IEnumerable<ECDSAPublicKey> ecdsaPublicKeys
        )
        {
            N = n;
            F = f;
            TpkePublicKey = tpkePublicKey;
            ThresholdSignaturePublicKeySet = thresholdSignaturePublicKeySet;
            _ecdsaPublicKeys = ecdsaPublicKeys.ToList();
            _tpkeVerificationKeys = tpkeVerificationKeys.ToList();
        }
        
        public int GetValidatorIndex(ECDSAPublicKey publicKey)
        {
            return EcdsaPublicKeySet
                .Select((key, index) => new {key, index})
                .Where(arg => publicKey.Equals(arg.key))
                .Select(arg => arg.index)
                .DefaultIfEmpty(-1)
                .First();
        }

        public PublicKey? GetTpkeVerificationKey(int playerIdx)
        {
            try
            {
                return _tpkeVerificationKeys[playerIdx];
            }
            catch (Exception e)
            {
                return null;
            }
        }
    }
}