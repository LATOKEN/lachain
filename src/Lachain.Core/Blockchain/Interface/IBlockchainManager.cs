using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Core.Blockchain.Interface
{
    public interface IBlockchainManager
    {
        bool TryBuildGenesisBlock();

        UInt256 CalcStateHash(Block block, IEnumerable<TransactionReceipt> transactionReceipts);
        
        void PersistBlockManually(Block block, IEnumerable<TransactionReceipt> transactions);
    }
}