using System;
using System.Collections.Generic;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Cryptography
{
    [Serializable]
    public class MultiSig
    {
        public static readonly MultiSig Zero = new MultiSig(0, new HashSet<PublicKey>());
        
        public uint Quorum { get; }
        public uint Total => (uint) _validators.Count;

        private readonly Dictionary<PublicKey, Signature> _signatures = new Dictionary<PublicKey, Signature>();
        private readonly HashSet<PublicKey> _validators;
        
        public MultiSig(uint quorum, HashSet<PublicKey> validators)
        {
            Quorum = quorum;
            _validators = validators;
        }
        
        public bool Sign(byte[] signature, byte[] message)
        {
            if (_signatures.Count >= Total)
                return false;
            var publicKey = new PublicKey(Crypto.Default.RecoverSignature(signature, message, true));
            if (!_validators.Contains(publicKey))
                return false;
            if (_signatures.ContainsKey(publicKey))
                return false;
            _signatures[publicKey] = new Signature(signature);
            return true;
        }

        public bool Verify(byte[] message)
        {
            if (_signatures.Count < Quorum)
                return false;
            foreach (var entry in _signatures)
            {
                if (!Crypto.Default.VerifySignature(message, entry.Value.Bytes, entry.Key.EncodedData))
                    return false;
            }
            return true;
        }
    }
}