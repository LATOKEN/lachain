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
        /// empty UInt256 number is 32 zero bytes 
        /// </summary>
        public static readonly UInt256 Zero = new byte[32].ToUInt256();

        public static bool IsZero(this UInt256 value)
        {
            return Zero.Equals(value);
        }

        public static BigInteger ToBigInteger(this UInt256 value)
        {
            return new BigInteger(value.ToBytes().Reverse().Concat(new byte[] {0}).ToArray());
        }

        public static UInt256 ToUInt256(this int value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            return ((BigInteger) value).ToUInt256();
        }

        public static UInt256 ToUInt256(this long value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            return ((BigInteger) value).ToUInt256();
        }

        public static UInt256 ToUInt256(this BigInteger value)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            var bytes = value.ToByteArray().Reverse().ToArray();
            if (bytes.Length > 33 || bytes.Length == 33 && bytes[0] != 0)
                throw new ArgumentOutOfRangeException(nameof(value));
            if (bytes.Length == 33)
                bytes = bytes.Skip(1).ToArray();
            var paddedBytes = (new byte[Math.Max(32 - bytes.Length, 0)]).Concat(bytes).ToArray();
            return paddedBytes.ToUInt256();
        }

        public static Money ToMoney(this UInt256 value)
        {
            return new Money(value);
        }

        public static byte[] ToBytes(this UInt256 value, bool stripTrailingZeros = false,
            bool stripLeadingZeros = false)
        {
            if (!stripTrailingZeros && !stripLeadingZeros) return value.Buffer.ToByteArray();
            if (stripLeadingZeros)
            {
                var idx = 0;
                while (idx < value.Buffer.Length && value.Buffer[idx] == 0) ++idx;
                return value.Buffer.Skip(idx).ToArray();
            }
            else
            {
                var idx = value.Buffer.Length - 1;
                while (idx >= 0 && value.Buffer[idx] == 0) --idx;
                return value.Buffer.Take(idx + 1).ToArray();
            }
        }

        public static UInt256 ToUInt256(this byte[] buffer, bool addLeadingZeros = false)
        {
            if (buffer.Length == 0) buffer = Zero.ToBytes();

            if (!addLeadingZeros && buffer.Length != 32 || buffer.Length > 32)
                throw new ArgumentOutOfRangeException(nameof(buffer));

            var padded = addLeadingZeros ? Enumerable.Repeat((byte) 0, 32 - buffer.Length).Concat(buffer) : buffer;
            return new UInt256 {Buffer = ByteString.CopyFrom(padded.ToArray())};
        }
    }
}