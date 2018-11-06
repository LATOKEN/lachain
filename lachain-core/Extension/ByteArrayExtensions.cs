using System;
using System.Collections.Generic;
using System.Text;

namespace Phorkus.Core.Extension
{
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Generate SHA256 digests
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="offset">Offset</param>
        /// <param name="count">Count</param>
        /// <returns>Return SHA256 digests</returns>
        public static byte[] Sha256(this byte[] value, int offset, int count)
        {
            return Crypto.Default.Sha256(value, offset, count);
        }

        /// <summary>
        /// Generate SHA256 digests
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Return SHA256 digests</returns>
        public static byte[] Sha256(this byte[] value)
        {
            return Crypto.Default.Sha256(value);
        }

        /// <summary>
        /// Generate SHA256 hash
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="offset">Offset</param>
        /// <param name="count">Count</param>
        /// <returns>Return SHA256 hash</returns>
        public static byte[] Hash256(this byte[] value, int offset, int count)
        {
            return Crypto.Default.Hash256(value, offset, count);
        }

        /// <summary>
        /// Generate SHA256 hash
        /// </summary>
        /// <param name="value">Value</param>
        /// <returns>Return SHA256 hash</returns>
        public static byte[] Hash256(this byte[] value)
        {
            return Crypto.Default.Hash256(value);
        }

        /// <summary>
        /// Bytarray XOR
        /// </summary>
        /// <param name="x">X</param>
        /// <param name="y">Y</param>
        /// <returns>Return XOR bytearray</returns>
        public static byte[] XOR(this byte[] x, byte[] y)
        {
            if (y == null) throw new ArgumentNullException(nameof(y));
            if (x.Length != y.Length) throw new ArgumentException(nameof(y));

            var result = new byte[x.Length];
            for (var i = 0; i < x.Length; i++)
            {
                result[i] = (byte)(x[i] ^ y[i]);
            }

            return result;
        }

        /// <summary>
        /// Convert to Hex String
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="usePrefix">Append 0x hex prefix</param>
        /// <returns>String</returns>
        public static string ToHexString(this IEnumerable<byte> value, bool usePrefix = false)
        {
            var sb = new StringBuilder();
            foreach (var b in value)
                sb.AppendFormat("{0:x2}", b);
            if (!usePrefix)
                return sb.ToString();
            if (sb.Length > 0)
                return "0x" + sb;
            return sb.ToString();
        }
    }
}
