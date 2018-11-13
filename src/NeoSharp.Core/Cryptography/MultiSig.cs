using System.Collections.Generic;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Cryptography
{
    [BinaryTypeSerializer(typeof(MultiSigBinarySerializer))]
    public class MultiSig
    {
        public static readonly MultiSig Zero = new MultiSig(0, new HashSet<PublicKey>());
        
        public uint Quorum { get; internal set; }
        public uint Total => (uint) _validators.Count;

        public Dictionary<PublicKey, Signature> Signatures => _signatures;
        public HashSet<PublicKey> Validators => _validators;
        
        private readonly Dictionary<PublicKey, Signature> _signatures = new Dictionary<PublicKey, Signature>();
        private readonly HashSet<PublicKey> _validators = new HashSet<PublicKey>();

        public MultiSig()
        {
            /* for deserialization */
        }
        
        public MultiSig(uint quorum, HashSet<PublicKey> validators)
        {
            Quorum = quorum;
            _validators = validators;
        }

        public void Deserialize(IBinarySerializer deserializer, BinaryReader reader)
        {
            Quorum = reader.ReadUInt32();

            var signatureCount = reader.ReadInt32();
            for (var i = 0; i < signatureCount; i++)
            {
                var publicKey = deserializer.Deserialize<PublicKey>(reader);
                var signature = deserializer.Deserialize<Signature>(reader);
                _signatures[publicKey] = signature;
            }

            var validatorCount = reader.ReadInt32();
            for (var i = 0; i < validatorCount; i++)
            {
                _validators.Add(deserializer.Deserialize<PublicKey>(reader));
            }
        }
        
        public int Serialize(IBinarySerializer serializer, BinaryWriter writer)
        {
            var result = 0;
            
            writer.Write(Quorum);
            result += 4;

            writer.Write(Signatures.Count);
            result += 4;
            foreach (var entry in Signatures)
            {
                result += serializer.Serialize(entry.Key, writer);
                result += serializer.Serialize(entry.Value, writer);
            }

            writer.Write(Validators.Count);
            result += 4;
            foreach (var publicKey in Validators)
            {
                result += serializer.Serialize(publicKey, writer);                
            }
            
            return result;
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