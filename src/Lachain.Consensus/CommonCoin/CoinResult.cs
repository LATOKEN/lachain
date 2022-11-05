using System;
using System.Linq;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.CommonCoin
{
    public class CoinResult : IEquatable<CoinResult>, IByteSerializable
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

        public bool Equals(CoinResult other)
        {
            if (ReferenceEquals(null, other)) return false;
            return ReferenceEquals(this, other) || RawBytes.SequenceEqual(other.RawBytes);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((CoinResult) obj);
        }

        public override int GetHashCode()
        {
            return RawBytes.Aggregate(0, (i, b) => i * 31 + b);
        }

        public byte[] ToByteArray()
        {
            return RawBytes;
        }

        public static CoinResult FromByteArray(byte[] bytes)
        {
            return new CoinResult(bytes);
        }
    }
}