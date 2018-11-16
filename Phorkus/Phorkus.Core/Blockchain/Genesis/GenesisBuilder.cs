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
        
        private BlockWithTransactions _genesisBlock;
        
        public BlockWithTransactions Build()
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var governingToken = _genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction();
            var minerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();
            
            var genesisTimestamp = new DateTime(kind: DateTimeKind.Utc,
                year: 2019, month: 1, day: 1, hour: 00, minute: 00, second: 00).ToTimestamp();
            
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
                Nonce = GenesisConsensusData
            };
            var result = new Block
            {
                Hash = header.ToHash256(),
                Header = header
            };
            result.Header.TransactionHashes.AddRange(genesisTransactions.Select(tx => tx.Hash).ToArray());
            
            /* genesis block transactions don't have signatures */
            var signed = genesisTransactions.Select(tx => new SignedTransaction
            {
                Transaction = tx.Transaction,
                Hash = tx.Hash,
                Signature = SignatureUtils.Zero
            });
            _genesisBlock = new BlockWithTransactions(result, signed.ToArray());
            return _genesisBlock;
        }
    }
}