using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.RLP;

namespace Lachain.Utility
{
    public class ConsensusState
    {
        public ConsensusState(byte[] tpkePublicKey, ValidatorCredentials[] validators)
        {
            TpkePublicKey = tpkePublicKey;
            Validators = validators;
        }

        public byte[] TpkePublicKey { get; }
        public ValidatorCredentials[] Validators { get; }

        public byte[] ToBytes()
        {
            var a = new List<byte[]> {TpkePublicKey};
            a.AddRange(Validators.Select(c => c.ToBytes()));
            return RLP.EncodeList(a.Select(RLP.EncodeElement).ToArray());
        }

        public static ConsensusState FromBytes(ReadOnlySpan<byte> bytes)
        {
            var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            var tpkePubKey = decoded[0].RLPData;
            var credentials = decoded.Skip(1)
                .Select(x => x.RLPData)
                .Select(x => ValidatorCredentials.FromBytes(x))
                .ToArray();
            return new ConsensusState(tpkePubKey, credentials);
        }
    }
}