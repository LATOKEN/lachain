using System;
using System.Globalization;
using System.Text;

namespace Phorkus.CrossChain
{
    public static class Utils
    {
        public const int LongHexLength = 16;
        public const int IntHexLength = 8;

        public static string ConvertIntToReversedHex(int data, int totalLength)
        {
            var reversedBytes = System.Net.IPAddress.NetworkToHostOrder(data);
            var hex = reversedBytes.ToString("x2");
            var trimmed = hex.Substring(0, hex.Length - 2);
            var trimmedLen = trimmed.Length;
            for (var i = 0; i < IntHexLength - trimmedLen; ++i)
            {
                trimmed += "00";
            }

            return trimmed;
        }

        public static string ConvertUIntToReversedHex(uint data, int totalLength)
        {
            var reversedBytes = System.Net.IPAddress.NetworkToHostOrder(data);
            var hex = reversedBytes.ToString("x2");
            var trimmed = hex.Substring(0, hex.Length - 2);
            var trimmedLen = trimmed.Length;
            for (var i = 0; i < IntHexLength - trimmedLen; ++i)
            {
                trimmed += "00";
            }

            return trimmed;
        }

        public static string ConvertByteArrayToString(byte[] bytes)
        {
            var hex = new StringBuilder(bytes.Length << 1);
            foreach (var b in bytes)
            {
                hex.AppendFormat("{0:x2}", b);
            }

            return hex.ToString();
        }

        public static string AppendZero(string hex)
        {
            if (hex.Length % 2 == 1)
            {
                hex = "0" + hex;
            }

            return hex;
        }

        public static string ConvertLongToReversedHex(long data)
        {
            var reversedBytes = System.Net.IPAddress.NetworkToHostOrder(data);
            var hex = reversedBytes.ToString("x2");
            var trimmed = hex.Substring(0, hex.Length - 2);
            var trimmedLen = trimmed.Length;
            for (var i = 0; i < LongHexLength - trimmedLen; ++i)
            {
                trimmed += "00";
            }

            return trimmed;
        }

        public static byte[] ConvertHexStringToByteArray(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    "The binary key cannot have an odd number of digits: {0}", hexString));
            }
            
            var data = new byte[hexString.Length >> 1];
            for (var index = 0; index < data.Length; index++)
            {
                var byteValue = hexString.Substring(index << 1, 2);
                data[index] = byte.Parse(byteValue, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            }

            return data;
        }

        public static string ReverseHex(string num)
        {
            var number = Convert.ToInt32(num, 16);
            var bytes = BitConverter.GetBytes(number);
            var retval = "";
            foreach (var b in bytes)
            {
                retval += b.ToString("x2");
            }

            return retval;
        }
    }
}