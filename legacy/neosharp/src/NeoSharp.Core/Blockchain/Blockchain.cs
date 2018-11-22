using System;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Core.Blockchain.Genesis;
using NeoSharp.Core.Blockchain.Processing.BlockProcessing;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain
{
    public class Blockchain : IBlockchain, IDisposable
    {
        private readonly IBlockProcessor _blockProcessor;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockRepository _blockRepository;
        private readonly IGlobalRepository _globalRepository;
        private readonly ISigner<BlockHeader> _blockSigner;

        private int _initialized;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="blockProcessor">Block Processor</param>
        /// <param name="blockchainContext">Block chain context class.</param>
        /// <param name="genesisBuilder">Genesis block generator.</param>
        /// <param name="blockRepository">Repository to working with blockchain.</param>
        public Blockchain(
            IBlockProcessor blockProcessor,
            IBlockchainContext blockchainContext,
            IGenesisBuilder genesisBuilder, 
            IBlockRepository blockRepository,
            IGlobalRepository globalRepository,
            ISigner<BlockHeader> blockSigner)
        {
            _blockProcessor = blockProcessor ?? throw new ArgumentNullException(nameof(blockProcessor));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _genesisBuilder = genesisBuilder ?? throw new ArgumentNullException(nameof(genesisBuilder));
            _blockRepository = blockRepository ?? throw new ArgumentNullException(nameof(blockRepository));
            _globalRepository = globalRepository ?? throw new ArgumentNullException(nameof(globalRepository));
            _blockSigner = blockSigner;
        }

        public void InitializeBlockchain()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0)
                return;
            
            var blockHeight = _globalRepository.GetTotalBlockHeight();
            var blockHeaderHeight = _globalRepository.GetTotalBlockHeaderHeight();
            
            _RefreshContext(blockHeight, blockHeaderHeight);

            _blockProcessor.Run();
            
            if (_blockchainContext.CurrentBlock == null || _blockchainContext.LastBlockHeader == null)
            {
                var genesisBlock = _genesisBuilder.Build();
                if (genesisBlock.Index != 0)
                    throw new Exception("Invalid genesis block height specified, must be 0");
                _blockProcessor.AddBlock(genesisBlock);
                _blockProcessor.WaitUntilBlockProcessed(genesisBlock.Index);
                _RefreshContext(blockHeight, blockHeaderHeight);
            }
        }

        private void _RefreshContext(uint blockHeight, uint blockHeaderHeight)
        {
            _blockchainContext.LastBlockHeader = _blockRepository.GetBlockHeaderByHeight(blockHeaderHeight);
            if (_blockchainContext.LastBlockHeader != null)
                _blockSigner.Sign(_blockchainContext.LastBlockHeader);
            _blockchainContext.CurrentBlock = _blockRepository.GetBlockHeaderByHeight(blockHeight);
            if (_blockchainContext.CurrentBlock != null)
                _blockSigner.Sign(_blockchainContext.CurrentBlock);
        }
        
        /// <inheritdoc />
        public void Dispose()
        {
            if (_initialized == 1)
            {
            }
        }
    }
}
