using System;
using System.Linq;
using Google.Protobuf.WellKnownTypes;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Cryptography;
using Phorkus.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisBuilder : IGenesisBuilder
    {
        public const ulong GenesisConsensusData = 2083236893UL;

        private readonly IGenesisAssetsBuilder _genesisAssetsBuilder;
        private readonly ICrypto _crypto;
        private readonly ITransactionManager _transactionManager;

        public GenesisBuilder(
            IGenesisAssetsBuilder genesisAssetsBuilder,
            ICrypto crypto,
            ITransactionManager transactionManager)
        {
            _genesisAssetsBuilder = genesisAssetsBuilder;
            _crypto = crypto;
            _transactionManager = transactionManager;
        }

        private BlockWithTransactions _genesisBlock;

        public BlockWithTransactions Build(KeyPair keyPair)
        {
            if (_genesisBlock != null)
                return _genesisBlock;

            var address = _crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            
            var governingToken = _genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction(address);
            var minerTransaction = _genesisAssetsBuilder.BuildGenesisMinerTransaction();

            var genesisTimestamp = new DateTime(kind: DateTimeKind.Utc,
                year: 2019, month: 1, day: 1, hour: 00, minute: 00, second: 00).ToTimestamp();

            /* distribute tokens (1 million for each holder) */
            var tokenDistribution = _genesisAssetsBuilder.IssueTransactionsToOwners(
                Money.FromDecimal(1_000_000m), governingToken.Register.ToHash160()
            );

            var txsBefore = new[]
            {
                /* first transaction is always a miner transaction */
                minerTransaction,
                /* creates NEO */
                governingToken
            };
            var genesisTransactions = txsBefore.Concat(tokenDistribution).ToArray();
            
            var nonce = 0ul;
            foreach (var tx in genesisTransactions)
            {
                tx.Signature = SignatureUtils.Zero;
                tx.From = address;
                tx.Nonce = nonce++;
            }
            
            var signed = genesisTransactions.Select(tx => _transactionManager.Sign(tx, keyPair));
            var signedTransactions = signed as SignedTransaction[] ?? signed.ToArray();
            var txHashes = signedTransactions.Select(tx => tx.Hash).ToArray();
            
            var header = new BlockHeader
            {
                Version = 0,
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = MerkleTree.ComputeRoot(txHashes),
                Timestamp = (ulong) genesisTimestamp.Seconds,
                Index = 0,
                Nonce = GenesisConsensusData
            };
            header.TransactionHashes.AddRange(txHashes);
            
            var result = new Block
            {
                Hash = header.ToHash256(),
                Header = header
            };
            
            _genesisBlock = new BlockWithTransactions(result, signedTransactions.ToArray());
            return _genesisBlock;
        }
    }
}