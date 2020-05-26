using System;
using System.Linq;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Crypto.ThresholdSignature
{
    public class Signature : IFixedWidth
    {
        private readonly G2 _signature;

        internal Signature(G2 signature)
        {
            _signature = signature;
        }

        public G2 RawSignature => _signature;

        public bool Parity()
        {
            var p = _signature.ToBytes().Aggregate(0u, (i, b) => i ^ b, x => x);
            return BitsUtils.Popcount(p) % 2 == 1;
        }

        public static int Width()
        {
            return G2.Width();
        }

        public void Serialize(Memory<byte> bytes)
        {
            _signature.Serialize(bytes);
        }

        public static Signature FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return new Signature(G2.FromBytes(bytes));
        }
    }
}