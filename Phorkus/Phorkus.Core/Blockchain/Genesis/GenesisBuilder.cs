using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        public const ulong GenesisConsensusData = 2083236893UL;
        
        private readonly IGenesisAssetsBuilder _genesisAssetsBuilder;
        
        public GenesisBuilder(IGenesisAssetsBuilder genesisAssetsBuilder)
        {
            _genesisAssetsBuilder = genesisAssetsBuilder;
        }
        
        private Block _genesisBlock;
        
        public Block Build()
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var governingToken = _genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction();
            var minerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();
            
            var genesisTimestamp = new DateTime(2016, 7, 15, 15, 8, 21, DateTimeKind.Utc).ToTimestamp();
            
            /* distribute tokens (1 million for each holder) */
            var tokenDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(Fixed256Utils.FromDecimal(1_000_000), governingToken.Transaction.ToHash160());
            
            var txsBefore = new[]
            {
                /* first transaction is always a miner transaction */
                minerTransaction,
                /* creates NEO */
                governingToken
            };
            var genesisTransactions = txsBefore.Concat(tokenDistribution).ToArray();

            var header = new BlockHeader
            {
                Version = 1,
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = null,
                Timestamp = (ulong) genesisTimestamp.Seconds,
                Index = 0,
                Type = HeaderType.Extended,
                Nonce = GenesisConsensusData,
                Multisig = new MultiSig()
            };
            var result = new Block
            {
                Hash = header.ToHash256(),
                Header = header
            };
            result.Header.TransactionHashes.AddRange(genesisTransactions.Select(tx => tx.Hash).ToArray());
            result.Transactions.AddRange(genesisTransactions.Select(tx => tx.Transaction).ToArray());
            
            _genesisBlock = result;
            return _genesisBlock;
        }
    }
}