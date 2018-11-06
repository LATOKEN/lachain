using System;
using System.Linq;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManger;
using NeoSharp.Types;

namespace NeoSharp.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        #region Private Fields 

        private Block _genesisBlock;
        private readonly IGenesisAssetsBuilder _genesisAssetsBuilder;
        private readonly ISigner<Block> _blockSigner;

        #endregion

        #region Constructor

        public GenesisBuilder(IGenesisAssetsBuilder genesisAssetsBuilder, ISigner<Block> blockSigner)
        {
            _genesisAssetsBuilder = genesisAssetsBuilder;
            _blockSigner = blockSigner;

            BinarySerializer.RegisterTypes(typeof(Transaction).Assembly, typeof(BlockHeader).Assembly);
        }

        #endregion

        #region IGenesisBuilder implementation
        
        public const ulong GenesisConsensusData = 2083236893UL;
        
        public Block Build()
        {
            if (_genesisBlock != null) return _genesisBlock;

            var governingToken = _genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction();
            var utilityToken = _genesisAssetsBuilder.BuildUtilityTokenRegisterTransaction();

            var genesisMinerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();
            var genesisIssueTransaction = _genesisAssetsBuilder.BuildGenesisIssueTransaction();

            var genesisWitness = _genesisAssetsBuilder.BuildGenesisWitness();
            var genesisTimestamp = new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc).ToTimestamp();
            
            /* create multisig trasnaction with 1 million tokens */
            var nextConsensusAddress = _genesisAssetsBuilder.BuildGenesisNextConsensusAddress();
            
            /* distribute tokens (1 million for each holder) */
            var governingDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(governingToken.Hash, Fixed8.FromDecimal(1_000_000));
            var utilityDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(utilityToken.Hash, Fixed8.FromDecimal(1_000_000));

            var txs = new Transaction[]
            {
                /* first transaction is always a miner transaction */
                genesisMinerTransaction,
                /* creates NEO */
                governingToken,
                /* creates GAS */
                utilityToken,
                /* cend all NEO to seed contract */
                genesisIssueTransaction
            };
            var genesisTransactions = txs.Concat(governingDistribution).Concat(utilityDistribution).ToArray();
            
            _genesisBlock = new Block
            {
                PreviousBlockHash = UInt256.Zero,
                Timestamp = genesisTimestamp,
                Index = 0,
                ConsensusData = GenesisConsensusData,
                NextConsensus = nextConsensusAddress,
                Witness = genesisWitness,
                Transactions = genesisTransactions
            };

            _blockSigner.Sign(_genesisBlock);
            return _genesisBlock;
        }

        #endregion
    }
}