using System;
using System.Collections.Generic;
using System.Linq;

namespace Phorkus.Crypto.ThresholdSignature
{
    public class ThresholdSigner : IThresholdSigner
    {
        private readonly byte[] _dataToSign;
        private readonly PrivateKeyShare _privateKeyShare;
        private readonly PublicKeySet _publicKeySet;
        private readonly SignatureShare[] _collectedShares;
        private Signature _signature;
        private int _collectedSharesNumber;

        public ThresholdSigner(IEnumerable<byte> dataToSign, PrivateKeyShare privateKeyShare, PublicKeySet publicKeySet)
        {
            _dataToSign = dataToSign.ToArray();
            _privateKeyShare = privateKeyShare;
            _publicKeySet = publicKeySet;
            _collectedShares = new SignatureShare[publicKeySet.Count];
            _collectedSharesNumber = 0;
            _signature = null;
        }

        public SignatureShare Sign()
        {
            return _privateKeyShare.HashAndSign(_dataToSign);
        }

        public bool AddShare(PublicKeyShare pubKey, SignatureShare sigShare, out Signature result)
        {
            result = null;
            var idx = _publicKeySet.GetIndex(pubKey);
            if (idx < 0 || idx >= _publicKeySet.Count) return false;
            if (_collectedShares[idx] != null) return false;
            if (!IsShareValid(pubKey, sigShare)) return false;
            if (_collectedSharesNumber > _publicKeySet.Threshold)
            {
                result = _signature;
                return true;
            }

            _collectedShares[idx] = sigShare;
            _collectedSharesNumber += 1;
            if (_collectedSharesNumber <= _publicKeySet.Threshold) return true;
            var signature = _publicKeySet.AssembleSignature(
                _collectedShares.Select((share, i) => new KeyValuePair<int, SignatureShare>(i, share))
                    .Where(pair => pair.Value != null).ToArray()
            );
            if (!_publicKeySet.SharedPublicKey.ValidateSignature(signature, _dataToSign))
                throw new Exception("Fatal error: all shares are valid but combined signature is not");
            _signature = signature;
            return true;
        }

        private bool IsShareValid(PublicKeyShare pubKey, SignatureShare sigShare)
        {
            return pubKey.ValidateSignature(sigShare, _dataToSign);
        }
    }
}