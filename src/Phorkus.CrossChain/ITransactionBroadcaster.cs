namespace Phorkus.CrossChain
{
    public interface ITransactionBroadcaster
    {
        void BroadcastTransaction(RawTransaction rawTransaction);
    }
}