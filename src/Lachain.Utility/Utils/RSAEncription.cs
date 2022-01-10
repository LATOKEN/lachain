using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Lachain.Utility.Utils
{
    public static class RSAEncription
    {
        private static UnicodeEncoding _encoder = new UnicodeEncoding();

        public static string Decrypt(string data, string privateKey)
        {
            var rsa = new RSACryptoServiceProvider();
            var dataArray = data.Split(new char[] { ',' });
            byte[] dataBytes = new byte[dataArray.Length];

            for (int i = 0; i < dataArray.Length; i++)
            {
                dataBytes[i] = Convert.ToByte(dataArray[i]);
            }

            rsa.FromXmlString(privateKey);
            var decryptedBytes = rsa.Decrypt(dataBytes, false);
            return _encoder.GetString(decryptedBytes);
        }

        public static string Encrypt(string data, string publicKey)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(publicKey);

            var databytes = _encoder.GetBytes(data);
            var encryptedBytes = rsa.Encrypt(databytes, false).ToArray();
            var length = encryptedBytes.Count();
            var item = 0;
            var sb = new StringBuilder();

            foreach (var x in encryptedBytes)
            {
                item++;
                sb.Append(x);

                if (item < length)
                    sb.Append(",");
            }

            return sb.ToString();
        }
    }
}
