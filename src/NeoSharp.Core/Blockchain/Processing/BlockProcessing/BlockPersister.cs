using System.Threading.Tasks;
using NeoSharp.Core.Blockchain.Processing.BlockHeaderProcessing;
using NeoSharp.Core.Blockchain.Processing.TranscationProcessing;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain.Processing.BlockProcessing
{
    public class BlockPersister : IBlockPersister
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IBlockHeaderPersister _blockHeaderPersister;
        private readonly ITransactionPersister<Transaction> _transactionPersister;
        private readonly ISigner<BlockHeader> _blockSigner;

        public BlockPersister(
            IGlobalRepository globalRepository,
            IBlockRepository blockRepository,
            IBlockchainContext blockchainContext,
            IBlockHeaderPersister blockHeaderPersister,
            ITransactionPersister<Transaction> transactionPersister,
            ISigner<BlockHeader> blockSigner)
        {
            _globalRepository = globalRepository;
            _blockRepository = blockRepository;
            _blockHeaderPersister = blockHeaderPersister;
            _blockchainContext = blockchainContext;
            _transactionPersister = transactionPersister;
            _blockSigner = blockSigner;
        }
        
        public async Task Persist(params Block[] blocks)
        {
            var height = await _globalRepository.GetTotalBlockHeight();

            foreach (var block in blocks)
            {
                var blockHeader = await _blockRepository.GetBlockHeaderByHash(block.Hash);
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
                    await _globalRepository.SetTotalBlockHeight(block.Index);
                    height = block.Index;
                }

                _blockSigner.Sign(block);
                _blockchainContext.CurrentBlock = block;
            }
        }
    }
}