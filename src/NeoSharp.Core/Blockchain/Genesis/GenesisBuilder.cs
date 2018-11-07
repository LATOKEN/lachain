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

            var minerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();
            var issueTransaction = _genesisAssetsBuilder.BuildGenesisIssueTransaction();

            var genesisWitness = _genesisAssetsBuilder.BuildGenesisWitness();
            var genesisTimestamp = new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc).ToTimestamp();
            
            /* create multisig trasnaction with 1 million tokens */
            var nextConsensusAddress = _genesisAssetsBuilder.BuildGenesisNextConsensusAddress();
            
            /* distribute tokens (1 million for each holder) */
            var tokenDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(Fixed8.FromDecimal(1_000_000), governingToken.Hash, utilityToken.Hash);
            
            var txsBefore = new Transaction[]
            {
                /* first transaction is always a miner transaction */
                minerTransaction,
                /* creates NEO */
                governingToken,
                /* creates GAS */
                utilityToken
            };
            var txsAfter = new Transaction[]
            {
                /* cend all NEO to seed contract */
                issueTransaction
            };
            var genesisTransactions = txsBefore.Concat(tokenDistribution).Concat(txsAfter).ToArray();
            
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
            _genesisBlock.Hash = UInt256.Parse("0x8b40748f0fc5ce35b65a813b49b0dbc1d7143003302ce2570e2d5fbb94deaada");
            
            /* TODO: "only for test period" */
            if (!_genesisBlock.Hash.ToString().Equals("0x8b40748f0fc5ce35b65a813b49b0dbc1d7143003302ce2570e2d5fbb94deaada"))
                throw new ArgumentException("Invalid genesis block");
            
            return _genesisBlock;
        }

        #endregion
    }
}