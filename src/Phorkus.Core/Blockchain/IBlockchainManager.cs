using System.Collections.Generic;
using Phorkus.Proto;

namespace Phorkus.Core.Blockchain
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock();

        UInt256 CalcStateHash(Block block, IEnumerable<TransactionReceipt> transactionReceipts);
        
        void PersistBlockManually(Block block, IEnumerable<TransactionReceipt> transactions);
    }
}