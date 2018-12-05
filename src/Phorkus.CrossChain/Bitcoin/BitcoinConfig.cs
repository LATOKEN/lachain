namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinConfig
    {
        public static readonly string Marker = "00";
        public static readonly string Flag = "01";
        public static readonly string Sequence = "feffffff";
        public static readonly string Version = "02000000";
        public static readonly string LockTime = "00000000";
        public static readonly string HashType = "01";
        public static readonly uint InputBytes = 64;
        public static readonly uint OutputBytes = 32;
        public static readonly uint TxDataBytes = 120;
        public static readonly string WitnessCode = "02";
    }
}