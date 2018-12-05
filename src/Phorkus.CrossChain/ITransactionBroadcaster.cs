namespace Phorkus.CrossChain
{
    public interface ITransactionBroadcaster
    {
        void BroadcastTransaction(ITransactionData transactionData);
    }
}