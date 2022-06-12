namespace Lachain.Consensus.CommonCoin
{
    public static class CoinToss
    {
        /*
         RootProtocol requests for one CommonCoin protocol
         BinaryAgreement requests many CommonCoin protocols sequentially
         For each odd epoch BinaryAgreements takes false, true, result of common coin
         So for epoch 1 => false, epoch 3 => true, epoch 5 => result of common coin
         and the cycle continues
        */
        public static long TossCoin(long epoch)
        {
            return (epoch / 2) % 3;
        }

        // epoch will be odd and TossCoin value will be 2
        public static bool CreateCoinId(long epoch)
        {
            return ((epoch & 1) != 0) && TossCoin(epoch) == 2;
        }
    }
}