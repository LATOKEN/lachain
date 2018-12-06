namespace Phorkus.CrossChain.Bitcoin
{
    public class BitcoinTransactionData : ITransactionData
    {
        public byte[] RawTransaction { get; set; }
    }
    
}