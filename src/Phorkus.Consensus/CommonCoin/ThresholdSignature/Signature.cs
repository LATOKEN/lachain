using System.Collections.Generic;
using System.Linq;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.CommonCoin.ThresholdSignature
{
    public class Signature
    {
        private G2 _signature;

        internal Signature(G2 signature)
        {
            _signature = signature;
        }

        public G2 RawSignature => _signature;

        public static Signature FromBytes(IEnumerable<byte> bytes)
        {
            var byteArray = bytes as byte[] ?? bytes.ToArray();
            return new Signature(G2.FromBytes(byteArray));
        }

        public IEnumerable<byte> ToBytes()
        {
            return RawSignature.ToBytes();
        }

        public bool Parity()
        {
            var p = _signature.ToBytes().Aggregate(0u, (i, b) => i ^ b, x => x);
            return BitsUtils.Popcount(p) % 2 == 1;
        }
    }
}