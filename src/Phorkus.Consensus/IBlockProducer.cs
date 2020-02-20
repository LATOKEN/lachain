using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Consensus
{
    public interface IBlockProducer
    {
        IEnumerable<TransactionReceipt> GetTransactionsToPropose();

        BlockHeader CreateHeader(IReadOnlyCollection<UInt256> txHashes, ECDSAPublicKey publicKey, ulong nonce);

        void ProduceBlock(IEnumerable<UInt256> txHashes, BlockHeader header, MultiSig multiSig);
    }
}