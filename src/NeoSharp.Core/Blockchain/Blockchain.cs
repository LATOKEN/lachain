using System;
using System.Threading;
using System.Threading.Tasks;
using NeoSharp.Core.Blockchain.Genesis;
using NeoSharp.Core.Blockchain.Processing.BlockProcessing;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.Core.Blockchain
{
    public class Blockchain : IBlockchain, IDisposable
    {
        private readonly IBlockProcessor _blockProcessor;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IGenesisBuilder _genesisBuilder;
        private readonly IBlockchainRepository _blockchainRepository;

        private int _initialized;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="blockProcessor">Block Processor</param>
        /// <param name="blockchainContext">Block chain context class.</param>
        /// <param name="genesisBuilder">Genesis block generator.</param>
        /// <param name="blockchainRepository">Repository to working with blockchain.</param>
        public Blockchain(
            IBlockProcessor blockProcessor,
            IBlockchainContext blockchainContext,
            IGenesisBuilder genesisBuilder, 
            IBlockchainRepository blockchainRepository)
        {
            _blockProcessor = blockProcessor ?? throw new ArgumentNullException(nameof(blockProcessor));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockchainContext));
            _genesisBuilder = genesisBuilder ?? throw new ArgumentNullException(nameof(genesisBuilder));
            _blockchainRepository = blockchainRepository ?? throw new ArgumentNullException(nameof(genesisBuilder));
        }

        public async Task InitializeBlockchain()
        {
            if (Interlocked.Exchange(ref _initialized, 1) != 0)
                return;

            var blockHeight = await _blockchainRepository.GetTotalBlockHeight();
            var blockHeaderHeight = await _blockchainRepository.GetTotalBlockHeaderHeight();
            
            _blockchainContext.LastBlockHeader = await _blockchainRepository.GetBlockHeaderByHeight(blockHeaderHeight);
            _blockchainContext.CurrentBlock = await _blockchainRepository.GetBlockHeaderByHeight(blockHeight);

            if (_blockchainContext.CurrentBlock == null || _blockchainContext.LastBlockHeader == null)
                await _blockProcessor.AddBlock(_genesisBuilder.Build());
            _blockProcessor.Run();
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
