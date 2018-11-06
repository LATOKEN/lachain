using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace Phorkus.Core.Extension
{
    public static class StringExtensions
    {
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
                if (!controller(str[c]))
                    continue;
                yield return (
                    Value: str.Substring(nextPiece, c - nextPiece),
                    Index: nextPiece
                );
                nextPiece = c + 1;
            }
            yield return (
                Value: str.Substring(nextPiece),
                Index: nextPiece
            );
        }

        /// <summary>
        /// Remove quote from string
        /// </summary>
        /// <param name="input">Input</param>
        /// <param name="quote">Quote</param>
        /// <returns>String</returns>
        public static string TrimMatchingQuotes(this string input, char quote)
        {
            if (input.Length >= 2 && input[0] == quote && input[input.Length - 1] == quote)
                return input.Substring(1, input.Length - 2);
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
                throw new ArgumentNullException(nameof(secureString));
            encoding = encoding ?? Encoding.UTF8;
            var unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = Marshal.SecureStringToGlobalAllocUnicode(secureString);
                var value = Marshal.PtrToStringUni(unmanagedString);
                if (value == null)
                    throw new Exception("Unable to unmarshal string");
                return encoding.GetBytes(value);
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                    Marshal.ZeroFreeGlobalAllocUnicode(unmanagedString);
            }
        }
    }
}