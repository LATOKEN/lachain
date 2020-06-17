using System;
using System.Linq;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;

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
            return G2.ByteSize;
        }

        public void Serialize(Memory<byte> bytes)
        {
            _signature.ToBytes().CopyTo(bytes);
        }

        public static Signature FromBytes(ReadOnlyMemory<byte> bytes)
        {
            return new Signature(G2.FromBytes(bytes.ToArray()));
        }
    }
}