using System.Threading.Tasks;
using NeoSharp.Core.Blockchain.Processing.BlockHeaderProcessing;
using NeoSharp.Core.Blockchain.Processing.TranscationProcessing;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.BlockProcessing
{
    public class BlockPersister : IBlockPersister
    {
        private readonly IBlockchainRepository _blockchainRepository;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockHeaderPersister _blockHeaderPersister;
        private readonly ITransactionPersister<Transaction> _transactionPersister;

        public BlockPersister(
            IBlockchainRepository blockchainRepository,
            IBlockchainContext blockchainContext,
            IBlockHeaderPersister blockHeaderPersister,
            ITransactionPersister<Transaction> transactionPersister)
        {
            _blockchainRepository = blockchainRepository;
            _blockchainContext = blockchainContext;
            _blockHeaderPersister = blockHeaderPersister;
            _transactionPersister = transactionPersister;
        }
        
        public async Task Persist(params Block[] blocks)
        {
            var height = await _blockchainRepository.GetTotalBlockHeight();

            foreach (var block in blocks)
            {
                var blockHeader = await _blockchainRepository.GetBlockHeaderByHash(block.Hash);
                if (blockHeader != null && blockHeader.Type == HeaderType.Extended)
                    continue;

                foreach (var transaction in block.Transactions)
                    await _transactionPersister.Persist(transaction);

                if (block.Index > 0)
                    await _blockHeaderPersister.Update(block.GetBlockHeader());
                else
                    await _blockHeaderPersister.Persist(block.GetBlockHeader());

                if (height + 1 == block.Index)
                {
                    await _blockchainRepository.SetTotalBlockHeight(block.Index);
                    height = block.Index;
                }

                _blockchainContext.CurrentBlock = block;
            }
        }
    }
}