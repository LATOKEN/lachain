using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Utility.Utils
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
            return value.Buffer.ToHex(prefix);
        }

        public static string ToHex(this UInt256 value, bool prefix = true)
        {
            return value.Buffer.ToHex(prefix);
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

        public static UInt256 HexToUInt256(this string buffer)
        {
            return buffer.HexToBytes().ToUInt256();
        }

        public static UInt160 HexToUInt160(this string buffer)
        {
            return buffer.HexToBytes().ToUInt160();
        }
    }
}