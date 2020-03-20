using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Consensus
{
    public interface IBlockProducer
    {
        IEnumerable<TransactionReceipt> GetTransactionsToPropose(long era);

        BlockHeader CreateHeader(
            ulong index, IReadOnlyCollection<UInt256> txHashes, ulong nonce, out UInt256[] hashesTaken
        );

        void ProduceBlock(IEnumerable<UInt256> txHashes, BlockHeader header, MultiSig multiSig);
    }
}