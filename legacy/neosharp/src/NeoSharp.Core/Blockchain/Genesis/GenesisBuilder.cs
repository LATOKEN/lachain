using System;
using System.Linq;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Models;
using NeoSharp.Core.Models.OperationManager;
using NeoSharp.Cryptography;

namespace NeoSharp.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        private readonly IGenesisAssetsBuilder _genesisAssetsBuilder;
        private readonly ISigner<Block> _blockSigner;

        private Block _genesisBlock;
        
        public GenesisBuilder(IGenesisAssetsBuilder genesisAssetsBuilder, ISigner<Block> blockSigner)
        {
            _genesisAssetsBuilder = genesisAssetsBuilder;
            _blockSigner = blockSigner;

            BinarySerializer.RegisterTypes(typeof(Transaction).Assembly, typeof(BlockHeader).Assembly);
        }
        
        public const ulong GenesisConsensusData = 2083236893UL;
        
        public Block Build()
        {
            if (_genesisBlock != null) return _genesisBlock;

            var governingToken = _genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction();
            var minerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();
            
            var genesisTimestamp = new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc).ToTimestamp();
            
            /* create multisig trasnaction with 1 million tokens */
            //var nextConsensusAddress = _genesisAssetsBuilder.BuildGenesisNextConsensusAddress();
            
            var hash = new UInt160(Crypto.Default.RIPEMD160(governingToken.Hash.ToArray()));
            
            /* distribute tokens (1 million for each holder) */
            var tokenDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(UInt256.FromDecimal(1_000_000), hash);
            
            var txsBefore = new Transaction[]
            {
                /* first transaction is always a miner transaction */
                minerTransaction,
                /* creates NEO */
                governingToken
            };
            var genesisTransactions = txsBefore.Concat(tokenDistribution).ToArray();
            
            _genesisBlock = new Block
            {
                PreviousBlockHash = UInt256.Zero,
                Timestamp = genesisTimestamp,
                Index = 0,
                Nonce = GenesisConsensusData,
                MultiSig = MultiSig.Zero,
                Transactions = genesisTransactions
            };
            _blockSigner.Sign(_genesisBlock);
            return _genesisBlock;
        }
    }
}