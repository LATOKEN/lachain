using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus
{
    public interface IBlockProducer
    {
        IEnumerable<TransactionReceipt> GetTransactionsToPropose(long era);

        BlockHeader? CreateHeader(
            ulong index, IReadOnlyCollection<TransactionReceipt> receipts, ulong nonce, out TransactionReceipt[] receiptsTaken
        );

        void ProduceBlock(IEnumerable<TransactionReceipt> receipts, BlockHeader header, MultiSig multiSig);
    }
}