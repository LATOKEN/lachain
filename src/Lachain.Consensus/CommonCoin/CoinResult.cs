using System;
using System.Linq;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.RLP;

namespace Lachain.Consensus.CommonCoin
{
    public class CoinResult : IEquatable<CoinResult>
    {
        public CoinResult(byte[] bytes)
        {
            RawBytes = bytes;
        }

        public byte[] RawBytes { get; }

        public bool Parity()
        {
            var p = RawBytes.Aggregate(0u, (i, b) => i ^ b, x => x);
            return BitsUtils.Popcount(p) % 2 == 1;
        }

        public bool Equals(CoinResult? other)
        {
            if (other is null) return false;
            return ReferenceEquals(this, other) || RawBytes.SequenceEqual(other.RawBytes);
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((CoinResult) obj);
        }

        public override int GetHashCode()
        {
            return RawBytes.Aggregate(0, (i, b) => i * 31 + b);
        }

        public byte[] ToBytes()
        {
            return RLP.EncodeList(RawBytes);
        }

        public static CoinResult FromBytes(ReadOnlyMemory<byte> bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var rawBytes = decoded[0].RLPData.AsReadOnlySpan().ToArray();

            return new CoinResult(rawBytes);
        }
    }
}