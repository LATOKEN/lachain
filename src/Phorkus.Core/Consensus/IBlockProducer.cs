using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public interface IBlockProducer
    {
        IEnumerable<TransactionReceipt> GetTransactionsToPropose();
        void ProduceBlock(IReadOnlyCollection<UInt256> txHashes, ECDSAPublicKey publicKey, ulong nonce);
    }
}