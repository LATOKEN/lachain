using System;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Phorkus.Core.Cryptography
{
    public static class Base58
    {
        /// <summary>
        /// base58 Alphabet
        /// </summary>
        private const string Alphabet58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

        /// <summary>
        /// Base
        /// </summary>
        private static readonly BigInteger Big58 = new BigInteger(58);

        public static byte[] CheckDecode(string input)
        {
            var buffer = Decode(input);
            if (buffer.Length < 4)
                throw new FormatException();
            var checksum = buffer.Sha256(0, buffer.Length - 4).Sha256();
            if (!buffer.Skip(buffer.Length - 4).SequenceEqual(checksum.Take(4)))
                throw new FormatException();
            return buffer.Take(buffer.Length - 4).ToArray();
        }

        public static string CheckEncode(byte[] data)
        {
            var checksum = data.Sha256().Sha256();
            var buffer = new byte[data.Length + 4];
            Buffer.BlockCopy(data, 0, buffer, 0, data.Length);
            Buffer.BlockCopy(checksum, 0, buffer, data.Length, 4);
            return Encode(buffer);
        }

        /// <summary>
        /// Decode
        /// </summary>
        /// <param name="input">String to be decoded</param>
        /// <returns>Decoded Byte Array</returns>
        public static byte[] Decode(string input)
        {
            var bi = BigInteger.Zero;
            for (var i = input.Length - 1; i >= 0; i--)
            {
                var index = Alphabet58.IndexOf(input[i]);
                if (index == -1)
                    throw new FormatException();
                bi += index * BigInteger.Pow(Big58, input.Length - 1 - i);
            }

            var bytes = bi.ToByteArray();
            Array.Reverse(bytes);
            var stripSignByte = bytes.Length > 1 && bytes[0] == 0 && bytes[1] >= 0x80;
            var leadingZeros = 0;
            for (var i = 0; i < input.Length && input[i] == Alphabet58[0]; i++)
                leadingZeros++;
            var tmp = new byte[bytes.Length - (stripSignByte ? 1 : 0) + leadingZeros];
            Array.Copy(bytes, stripSignByte ? 1 : 0, tmp, leadingZeros, tmp.Length - leadingZeros);
            return tmp;
        }

        /// <summary>
        /// Encode
        /// </summary>
        /// <param name="input">Byte Array to encode</param>
        /// <returns>Encoded string</returns>
        public static string Encode(byte[] input)
        {
            var value = new BigInteger(new byte[1].Concat(input).Reverse().ToArray());
            var sb = new StringBuilder();
            while (value >= Big58)
            {
                var mod = value % Big58;
                sb.Insert(0, Alphabet58[(int) mod]);
                value /= Big58;
            }

            sb.Insert(0, Alphabet58[(int) value]);
            foreach (var b in input)
            {
                if (b != 0)
                    break;
                sb.Insert(0, Alphabet58[0]);
            }

            return sb.ToString();
        }
    }
}