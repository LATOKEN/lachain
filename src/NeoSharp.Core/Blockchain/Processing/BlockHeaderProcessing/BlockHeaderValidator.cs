using NeoSharp.Core.Blockchain.Genesis;
using NeoSharp.Core.Models;

namespace NeoSharp.Core.Blockchain.Processing.BlockHeaderProcessing
{
    public class BlockHeaderValidator : IBlockHeaderValidator
    {
        private readonly IBlockchainContext _blockchainContext;
        private readonly IGenesisBuilder _genesisBuilder;
        
        public BlockHeaderValidator(IBlockchainContext blockchainContext, IGenesisBuilder genesisBuilder)
        {
            _blockchainContext = blockchainContext;
            _genesisBuilder = genesisBuilder;
        }

        public bool IsValid(BlockHeader blockHeader)
        {
            if (_blockchainContext.LastBlockHeader != null)
            {
                if (_blockchainContext.LastBlockHeader.Index + 1 != blockHeader.Index ||
                    _blockchainContext.LastBlockHeader.Hash != blockHeader.PreviousBlockHash)
                {
                    return false;
                }
            }
            else
            {
                /* TODO: "we have to think about it" */
                if (blockHeader.Index != 0 || blockHeader.Hash != _genesisBuilder.Build().Hash)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
