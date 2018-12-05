namespace Phorkus.CrossChain.Ethereum
{
    public class EthereumTransactionData : ITransactionData
    {
        public byte[] RawTransaction { get; set; }
    }
}