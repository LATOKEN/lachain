﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using NeoSharp.Core.Types;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Split a command line into enumerable
        ///     https://stackoverflow.com/a/24829691
        /// </summary>
        /// <param name="commandLine">Command line</param>
        /// <returns>Return the ienumerable result</returns>
        public static IEnumerable<CommandToken> SplitCommandLine(this string commandLine)
        {
            var inQuotes = false;
            var isEscaping = false;

            var reslist = commandLine.Split((c) =>
             {
                 if (c == '\\' && !isEscaping) { isEscaping = true; return false; }

                 if (c == '\"' && !isEscaping)
                     inQuotes = !inQuotes;

                 isEscaping = false;

                 return !inQuotes && char.IsWhiteSpace(c)/*c == ' '*/;
             });

            foreach (var (Value, Index) in reslist)
            {
                var cmd = new CommandToken(Value, Index, Value.Length);

                if (string.IsNullOrEmpty(cmd.Value)) continue;

                yield return cmd;
            }
        }

        /// <summary>
        /// Split string into enumerable
        /// </summary>
        /// <param name="str">String</param>
        /// <param name="controller">Controller Func</param>
        /// <returns></returns>
        public static IEnumerable<(string Value, int Index)> Split(this string str, Func<char, bool> controller)
        {
            var nextPiece = 0;

            for (var c = 0; c < str.Length; c++)
            {
                if (controller(str[c]))
                {
                    yield return (Value: str.Substring(nextPiece, c - nextPiece), Index: nextPiece);
                    nextPiece = c + 1;
                }
            }

            yield return (Value: str.Substring(nextPiece), Index: nextPiece);
        }

        /// <summary>
        /// Remove quote from string
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="quote">Quote</param>
        /// <returns>String</returns>
        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if ((input.Length >= 2) && (input[0] == quote) && (input[input.Length - 1] == quote))
            {
                return input.Substring(1, input.Length - 2);
            }

            return input;
        }

        /// <summary>
        /// Converts SecureString to byte array
        /// </summary>
        /// <param name="secureString">SecureString</param>
        /// <param name="encoding">Encoding</param>
        /// <returns>Byte Array</returns>
        public static byte[] ToByteArray(this SecureString secureString, Encoding encoding = null)
        {
            if (secureString == null)
            {
                throw new ArgumentNullException(nameof(secureString));
            }

            encoding = encoding ?? Encoding.UTF8;

            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);

                return encoding.GetBytes(Marshal.PtrToStringUni(unmanagedString));
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
                }
            }
        }

        /// <summary>
        /// Converts a Address into a ScriptHash representation 
        /// </summary>
        /// <param name="address">Address</param>
        /// <returns>The script hash.</returns>
        public static UInt160 ToScriptHash(this string address)
        {
            var buffer = Crypto.Default.Base58CheckDecode(address);

            if (buffer.Length != 21 || buffer[0] != 0x017)
                throw new ArgumentException(nameof(address));

            return new UInt160(buffer.Skip(1).ToArray());
        }

        /// <summary>
        /// Check string is in hex format.
        /// </summary>
        /// <returns>if hex in string was onlyed true, otherwise false.</returns>
        /// <param name="value">Value.</param>
        public static bool IsHexString(this string value)
        {
            return Regex.IsMatch(value, @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z");
        }

        /// <summary>
        /// Convert Hex string to byte array
        /// </summary>
        /// <param name="value">Value</param>
        /// <param name="limit">Limit</param>
        /// <returns>Byte Array</returns>
        public static byte[] HexToBytes(this string value, int limit = 0)
        {
            if (string.IsNullOrEmpty(value))
            {
                return new byte[0];
            }
 
            if (value.StartsWith("0x", StringComparison.InvariantCultureIgnoreCase))
            {
                value = value.Substring(2);
            }

            if (value.Length % 2 == 1)
            {
                throw new FormatException();
            }

            if (limit != 0 && value.Length != limit)
            {
                throw new FormatException();
            }

            var result = new byte[value.Length / 2];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = byte.Parse(value.Substring(i * 2, 2), NumberStyles.AllowHexSpecifier);
            }

            return result;
        }
    }
}