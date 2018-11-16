using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Utils
{
    public static class HexUtils
    {
        public static UInt256 HexToUInt256(this string buffer)
        {
            throw new NotImplementedException();
        }

        public static UInt256 HexToUInt256(this byte[] buffer)
        {
            throw new NotImplementedException();
        }

        public static UInt160 HexToUInt160(this string buffer)
        {
            throw new NotImplementedException();
        }

        public static UInt160 HexToUInt160(this byte[] buffer)
        {
            throw new NotImplementedException();
        }

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
        
        public static string ToHex(this IEnumerable<byte> buffer)
        {
            var sb = new StringBuilder();
            foreach (var b in buffer)
                sb.AppendFormat("{0:x2}", b);
            return $"0x{sb}";
        }
    }
}