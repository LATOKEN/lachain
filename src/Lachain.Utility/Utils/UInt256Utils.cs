using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class UInt256Utils
    {
        /// <summary>
        /// empty UInt256 number is 32 empty bytes 
        /// </summary>
        public static readonly UInt256 Zero = new byte[32].ToUInt256();

        public static bool IsZero(this UInt256 value)
        {
            return Zero.Equals(value);
        }

        public static BigInteger ToBigInteger(this UInt256 value, bool reversed = false)
        {
            var valueBytes = reversed ? value.ToBytes().Reverse().ToArray() : value.ToBytes();
            return new BigInteger(valueBytes.Concat(new byte[] {0}).ToArray());
        }

        public static UInt256 ToUInt256(this int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            return ((BigInteger) value).ToUInt256(true);
        }

        public static UInt256 ToUInt256(this BigInteger value, bool reversed = false)
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            var bytes = value.ToByteArray();
            if (bytes.Length > 33 || bytes.Length == 33 && bytes[32] != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            var paddedBytes = bytes.Take(32).Concat(new byte[Math.Max(32 - bytes.Length, 0)]).ToArray();
            return reversed ? paddedBytes.Reverse().ToArray().ToUInt256() : paddedBytes.ToUInt256();
        }

        public static Money ToMoney(this UInt256 value, bool reversed = false)
        {
            return new Money(value.ToBigInteger(reversed));
        }

        public static byte[] ToBytes(this UInt256 value, bool stripTrailingZeros = false)
        {
            if (!stripTrailingZeros) return value.Buffer.ToByteArray();
            var idx = value.Buffer.Length - 1;
            while (idx >= 0 && value.Buffer[idx] == 0) --idx;
            return value.Buffer.Take(idx + 1).ToArray();
        }

        public static UInt256 ToUInt256(this byte[] buffer, bool addTrailingZeros = false)
        {
            if (!addTrailingZeros && buffer.Length != 32 || buffer.Length > 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            return new UInt256
            {
                Buffer = ByteString.CopyFrom(buffer.Concat(Enumerable.Repeat((byte) 0, 32 - buffer.Length)).ToArray())
            };
        }
    }
}