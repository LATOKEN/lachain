namespace Lachain.Consensus.CommonCoin
{
    public static class CoinToss
    {
        // Starting epoch for which BinaryAgreement requests for CommonCoin
        public static long StartingEpochForCommonCoin = 5;
        public static long TossCoin(long epoch)
        {
            return (epoch / 2) % 3;
        }

        public static bool CreateCoinId(long epoch)
        {
            return TossCoin(epoch) == 2;
        }

        public static long PreviousEpoch(long epoch)
        {
            return epoch - 6;
        }
    }
}