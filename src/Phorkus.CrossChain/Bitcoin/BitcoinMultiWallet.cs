namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinMultiWallet : IMultiWallet
    {
        private readonly string _publicKey;

        public BitcoinMultiWallet(string publicKey)
        {
            _publicKey = publicKey;
        }

        public string GetPublicKey()
        {
            return _publicKey;
        }
    }
}