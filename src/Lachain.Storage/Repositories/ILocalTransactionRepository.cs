using Lachain.Proto;

namespace Lachain.Storage.Repositories
{
    public interface ILocalTransactionRepository
    {
        void SaveState(byte[] state);
        byte[] LoadState();
        UInt256[] GetTransactionHashes(ulong limit);
        void TryAddTransaction(TransactionReceipt receipt);
        void SetWatchAddress(UInt160 address);
    }
}