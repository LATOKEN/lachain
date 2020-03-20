namespace Phorkus.Crypto
{
    public class CryptoProvider
    {
        private static readonly ICrypto Crypto = new DefaultCrypto();

        public static ICrypto GetCrypto()
        {
            return Crypto;
        }
    }
}