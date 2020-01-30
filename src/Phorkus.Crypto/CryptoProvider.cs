namespace Phorkus.Crypto
{
    public class CryptoProvider
    {
        private static readonly ICrypto Crypto = new BouncyCastle();

        public static ICrypto GetCrypto()
        {
            return Crypto;
        }
    }
}