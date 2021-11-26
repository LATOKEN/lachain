using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Nethereum.Hex.HexTypes;
using Lachain.Proto;

namespace Lachain.Utility.Utils
{
    public static class HexUtils
    {
        public static byte[] HexToBytes(this string buffer, int limit = 0)
        {
            if (string.IsNullOrEmpty(buffer))
                return new byte[0];
            if (buffer.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
                buffer = buffer.Substring(2);
            if (buffer.Length % 2 == 1)
                throw new FormatException();
            if (limit != 0 && buffer.Length != limit)
                throw new FormatException();
            var result = new byte[buffer.Length / 2];
            for (var i = 0; i < result.Length; i++)
                result[i] = byte.Parse(buffer.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            return result;
        }

        public static string ToHex(this UInt160 value, bool prefix = true)
        {
            return value.ToBytes().ToHex(prefix);
        }

        public static string ToHex(this UInt256 value, bool prefix = true)
        {
            return value.ToBytes().ToHex(prefix);
        }
        public static string ToHex(this ECDSAPublicKey key, bool prefix = true)
        {
            return key.Buffer.ToHex(prefix);
        }
        
        public static string ToHex(this ECDSAPrivateKey key, bool prefix = true)
        {
            return key.Buffer.ToHex(prefix);
        }
        
        public static string ToHex(this Signature signature, bool prefix = true)
        {
            return signature.Buffer.ToHex(prefix);
        }

        public static string ToHex(this IEnumerable<byte> buffer, bool prefix = true)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
                sb.AppendFormat("{0:x2}", b);
            return prefix ? $"0x{sb}" : sb.ToString();
        }
        
        public static string ToTrimHex(this IEnumerable<byte> buffer, bool prefix = true)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
                sb.AppendFormat("{0:x2}", b);
            return prefix ? $"0x{sb.ToString().TrimStart('0')}" : sb.ToString();
        }

        public static UInt256 HexToUInt256(this string buffer)
        {
            return buffer.HexToBytes().ToUInt256();
        }

        public static UInt160 HexToUInt160(this string buffer)
        {
            return buffer.HexToBytes().ToUInt160();
        }

        public static ulong HexToUlong(this string buffer)
        {
            return ulong.Parse(buffer.Replace("0x", ""), NumberStyles.HexNumber);
        }

        public static string ToHex(this ulong num, bool evenBytesCount = true)
        {
            var res = num.ToHexBigInteger().HexValue;
            return evenBytesCount ? res.ToEvenBytesCount() : res;
        }

        public static string ToHex(this int num, bool evenBytesCount = true)
        {
            var res = num.ToHexBigInteger().HexValue;
            return evenBytesCount ? res.ToEvenBytesCount() : res;
        }

        public static byte[] TrimLeadingZeros(this byte[] array)
        {
            var firstIndex = Array.FindIndex(array, b => b != 0);
            return array.Skip(firstIndex).ToArray();
        }

        public static byte[] AddLeadingZeros(this byte[] array)
        {
            var zerosNeed = 32 - array.Length % 32;
            return Enumerable.Repeat((byte)0, zerosNeed % 32).Concat(array).ToArray();
        }

        public static byte[] AddTrailingZeros(this byte[] array)
        {
            var zerosNeed = 32 - array.Length % 32;
            return array.Concat(Enumerable.Repeat((byte)0, zerosNeed % 32)).ToArray();
        }

        public static byte[] EncodeString(this string str)
        {
            var prefix = "0x0000000000000000000000000000000000000000000000000000000000000020".HexToBytes();
            var hexValue = Encoding.ASCII.GetBytes(str);
            var len = hexValue.Length;

            return prefix.Concat(len.ToUInt256().ToBytes()).Concat(hexValue.AddTrailingZeros()).ToArray();
        }

        private static string ToEvenBytesCount(this string hex)
        {
            return hex.Length % 2 == 1 ? "0x0" + hex.Substring(2) : hex;
        }
    }
}