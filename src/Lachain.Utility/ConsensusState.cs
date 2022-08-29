using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Nethereum.RLP;

namespace Lachain.Utility
{
    public class ConsensusState
    {
        public ConsensusState(byte[] tpkePublicKey, byte[][] tpkeVerificationKeys, ValidatorCredentials[] validators)
        {
            TpkePublicKey = tpkePublicKey;
            TpkeVerificationKeys = tpkeVerificationKeys;
            Validators = validators;
        }

        public byte[] TpkePublicKey { get; }
        
        public byte[][] TpkeVerificationKeys { get; }
        public ValidatorCredentials[] Validators { get; }

        public byte[] ToBytes()
        {
            var a = new List<byte[]> {TpkePublicKey};
            a.Add(new byte[] {(byte)TpkeVerificationKeys.Length});
            a.AddRange(TpkeVerificationKeys);
            a.AddRange(Validators.Select(c => c.ToBytes()));
            return RLP.EncodeList(a.Select(RLP.EncodeElement).ToArray());
        }

        public static ConsensusState FromBytes(ReadOnlySpan<byte> bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var tpkePubKey = decoded[0].RLPData;
            if (decoded[1].RLPData.Length == 1) // new data format with verification keys
            {
                var keysNumber = decoded[1].RLPData[0];
                var tpkeVerificationKeys = decoded.Skip(2).Take(keysNumber)
                    .Select(x => x.RLPData)
                    .ToArray();
                var credentials = decoded.Skip(2 + keysNumber)
                    .Select(x => x.RLPData)
                    .Select(x => ValidatorCredentials.FromBytes(x))
                    .ToArray();
                return new ConsensusState(tpkePubKey, tpkeVerificationKeys, credentials);
            }
            // old data format without verification keys
            var old_credentials = decoded.Skip(1)
                .Select(x => x.RLPData)
                .Select(x => ValidatorCredentials.FromBytes(x))
                .ToArray();
            var fakeTpkeVerificationKeys = Enumerable.Range(0, old_credentials.Length)
                .Select(i => tpkePubKey).ToArray();
            return new ConsensusState(tpkePubKey, fakeTpkeVerificationKeys, old_credentials);
        }
    }
}