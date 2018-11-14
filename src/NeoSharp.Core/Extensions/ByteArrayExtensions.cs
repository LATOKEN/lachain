using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Extensions
{
    public static class ByteArrayExtensions
    {
        /// <summary>
        /// Generate Script Hash
        /// </summary>
        /// <param name="script">Script</param>
        /// <returns>ScriptHash</returns>
        public static UInt160 ToScriptHash(this byte[] script)
        {
            return new UInt160(Crypto.Default.Hash160(script));
        }

        public static UInt160 ToScriptHash(this UInt256 value)
        {
            return new UInt160(Crypto.Default.Hash160(value.ToArray()));
        }
        
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
        /// Convert Byte Array to Int32
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="startIndex">Start index</param>
        /// <returns>Int32</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int ToInt32(this byte[] value, int startIndex = 0)
        {
            return BitConverter.ToInt32(value, startIndex);
        }

        /// <summary>
        /// Convert Byte Array to UInt32
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="startIndex">Start index</param>
        /// <returns>UInt32</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint ToUInt32(this byte[] value, int startIndex = 0)
        {
            return BitConverter.ToUInt32(value, startIndex);
        }

        /// <summary>
        /// Convert to Hex String
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="append0X">Append 0x hex prefix</param>
        /// <returns>String</returns>
        public static string ToHexString(this IEnumerable<byte> value, bool append0X = false)
        {
            var sb = new StringBuilder();

            foreach (var b in value)
            {
                sb.AppendFormat("{0:x2}", b);
            }

            if (append0X && sb.Length > 0)
            {
                return $"0x{sb}";
            }

            return sb.ToString();
        }

        ///// <summary>
        ///// Convert to Hex String
        ///// </summary>
        ///// <param name="value">Value</param>
        ///// <param name="append0x">Append 0x hex prefix</param>
        ///// <returns>String</returns>
        //public static string ToHexString(this IEnumerable<byte> value, bool append0x = false)
        //{
        //    var sb = new StringBuilder();

        //    foreach (var b in value)
        //        sb.AppendFormat("{0:x2}", b);

        //    if (append0x)
        //    {
        //        if (sb.Length > 0) return "0x" + sb.ToString();
        //    }

        //    return sb.ToString();
        //}
    }
}
