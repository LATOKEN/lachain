using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IBlockProducer
    {
        IEnumerable<TransactionReceipt> GetTransactionsToPropose();

        BlockHeader CreateHeader(
            ulong index, IReadOnlyCollection<UInt256> txHashes, ulong nonce, out UInt256[] hashesTaken
        );

        void ProduceBlock(IEnumerable<UInt256> txHashes, BlockHeader header, MultiSig multiSig);
    }
}