namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinConfig
    {
        public const string Marker = "00";
        public const string Flag = "01";
        public const string Sequence = "feffffff";
        public const string Version = "02000000";
        public const string LockTime = "00000000";
        public const string HashType = "01";
        public const uint InputBytes = 64;
        public const uint OutputBytes = 32;
        public const uint TxDataBytes = 120;
        public const string WitnessCode = "02";
        public const int Decimals = 8;
    }
}