using System.Collections.Generic;
using System.Linq;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.RLP;
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
        //public byte[] SerializableByteObject { get; }

        public PublicConsensusKeySet(int n, int f,
            PublicKey tpkePublicKey,
            PublicKeySet thresholdSignaturePublicKeySet,
            IEnumerable<ECDSAPublicKey> ecdsaPublicKeys/*, byte[] serializableByteObject*/
        )
        {
            N = n;
            F = f;
            TpkePublicKey = tpkePublicKey;
            ThresholdSignaturePublicKeySet = thresholdSignaturePublicKeySet;
            _ecdsaPublicKeys = ecdsaPublicKeys.ToList();
        }

        public int GetValidatorIndex(ECDSAPublicKey publicKey)
        {
            return EcdsaPublicKeySet
                .Select((key, index) => new { key, index })
                .Where(arg => publicKey.Equals(arg.key))
                .Select(arg => arg.index)
                .DefaultIfEmpty(-1)
                .First();
        }

        public byte[] ToBytes()
        {
            var bytesArray = new List<byte[]>
            {
                N.ToBytes().ToArray(),
                F.ToBytes().ToArray(),
                TpkePublicKey.ToBytes().ToArray(),
                ThresholdSignaturePublicKeySet.ToBytes().ToArray(),
                _ecdsaPublicKeys.ToByteArray()
            };

            return RLP.EncodeList(bytesArray.Select(RLP.EncodeElement).ToArray());
        }

        public static IPublicConsensusKeySet FromBytes(byte[] bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var n = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var f = decoded[1].RLPData.AsReadOnlySpan().ToInt32();
            var tpkePublicKey = PublicKey.FromBytes(decoded[2].RLPData);
            var thresholdSignaturePublicKeySet = PublicKeySet.FromBytes(decoded[3].RLPData);
            var ecdsaPublicKeys = ProtoUtils.ToEcdsaPublicKeys(decoded[4].RLPData);
            
            return new PublicConsensusKeySet(n, f, tpkePublicKey, thresholdSignaturePublicKeySet, ecdsaPublicKeys);
        }
    }
}