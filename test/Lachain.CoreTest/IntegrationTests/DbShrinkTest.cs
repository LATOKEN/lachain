using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.DbCompact;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class DbShrinkTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private IDbShrink _dbOptimizer = null!;
        private IRocksDbContext _dbContext = null!;
        private IContainer? _container;

        public DbShrinkTest()
        {
           var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();

        } 

        [SetUp]
        public void Setup()
        {
            _container?.Dispose() ;
            TestUtils.DeleteTestChainData();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _configManager = _container.Resolve<IConfigManager>();
            _dbOptimizer = _container.Resolve<IDbShrink>();
            _dbContext = _container.Resolve<IRocksDbContext>();

            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
            }
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_DeletionOldSnapshot()
        {
            _blockManager.TryBuildGenesisBlock();
            AddSeveralBlocks(1000);
            ulong depth = 100;
            _dbOptimizer.ShrinkDb(depth, _blockManager.GetHeight(), true);
            var repos = Enum.GetValues(typeof(RepositoryType)).Cast<RepositoryType>();
            var lastBlock = _dbOptimizer.StartingBlockToKeep(depth, _blockManager.GetHeight());
            for (ulong fromBlock = _dbOptimizer.GetOldestSnapshotInDb(); fromBlock < lastBlock; fromBlock++)
            {
                foreach (var repo in repos)
                {
                    var prefix = EntryPrefix.SnapshotIndex.BuildPrefix(
                        ((uint) repo).ToBytes().Concat(fromBlock.ToBytes()).ToArray()
                    );
                    var version = _dbContext.Get(prefix);
                    Assert.AreEqual(null, version, $"{repo} for block {fromBlock} is not deleted");
                }
            }

            AddSeveralBlocks(100);
            var startingBlock = _dbOptimizer.GetOldestSnapshotInDb();
            depth = _dbOptimizer.StartingBlockToKeep(startingBlock, _blockManager.GetHeight());
            _dbOptimizer.ShrinkDb(depth, _blockManager.GetHeight(), true);
            _dbOptimizer.ShrinkDb(depth - 1, _blockManager.GetHeight(), true);
            lastBlock = _dbOptimizer.StartingBlockToKeep(depth, _blockManager.GetHeight());
            for (ulong fromBlock = _dbOptimizer.GetOldestSnapshotInDb(); fromBlock < lastBlock; fromBlock++)
            {
                foreach (var repo in repos)
                {
                    var prefix = EntryPrefix.SnapshotIndex.BuildPrefix(
                        ((uint) repo).ToBytes().Concat(fromBlock.ToBytes()).ToArray()
                    );
                    var version = _dbContext.Get(prefix);
                    Assert.AreEqual(null, version, $"{repo} for block {fromBlock} is not deleted");
                }
            }
        }

        private void AddSeveralBlocks(ulong blockCount)
        {
            var currentHeight = _blockManager.GetHeight();
            var persistedTx = 0;
            for (ulong iter = 0; iter < blockCount; iter++)
            {
                var topUpReceipts = new List<TransactionReceipt>();
                var randomReceipts = new List<TransactionReceipt>();
                var txCount = 10;
                var coverTxFeeAmount = Money.Parse("0.0000000001");
                for (var i = 0; i < txCount; i++)
                {
                    var tx = TestUtils.GetCustomTransaction("0", "0.000000000000000001", HardforkHeights.IsHardfork_9Active(currentHeight + 2));
                    randomReceipts.Add(tx);
                    topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                        (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i, 
                        HardforkHeights.IsHardfork_9Active(currentHeight + 1)));
                }

                foreach (var tx in topUpReceipts)
                {
                    var added = _transactionPool.Add(tx);
                    if (added != OperatingError.UnsupportedTransaction && added != OperatingError.Ok)
                    {
                        Assert.That(false, $"top up tx not added to pool {added}");
                    }
                }

                var takenTxes = _transactionPool.Peek(1000, 1000, currentHeight + 1);
                var block = BuildNextBlock(takenTxes.ToArray());
                var result = ExecuteBlock(block, takenTxes.ToArray());
                Assert.AreEqual(result, OperatingError.Ok);
                var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
                Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);
                currentHeight++;

                foreach (var tx in executedBlock.TransactionHashes)
                {
                    var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx);
                    if (!(receipt is null) && receipt.Status == TransactionStatus.Executed)
                        persistedTx++;
                }

                foreach (var tx in randomReceipts)
                {
                    var added = _transactionPool.Add(tx);
                    if (added != OperatingError.UnsupportedTransaction && added != OperatingError.Ok)
                    {
                        Assert.That(false, $"random tx not added to pool {added}");
                    }
                }

                takenTxes = _transactionPool.Peek(1000, 1000, currentHeight + 1);
                block = BuildNextBlock(takenTxes.ToArray());
                result = ExecuteBlock(block, takenTxes.ToArray());
                Assert.AreEqual(result, OperatingError.Ok);
                executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
                Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);
                currentHeight++;
                
                foreach (var tx in executedBlock.TransactionHashes)
                {
                    var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx);
                    if (!(receipt is null) && receipt.Status == TransactionStatus.Executed)
                        persistedTx++;
                }
            }

            Console.WriteLine($"Tx persisted {persistedTx} out of {blockCount * 20}");
        }

        private Block BuildNextBlock(TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(merkleRoot, predecessor, _stateManager.LastApprovedSnapshot.StateHash);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = {receipts.Select(tx => tx.Hash)},
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(UInt256 merkleRoot, Block? predecessor,
            UInt256 stateHash)
        {
            var blockIndex = predecessor!.Header.Index + 1;
            var header = new BlockHeader
            {
                Index = blockIndex,
                PrevBlockHash = predecessor!.Hash,
                MerkleRoot = merkleRoot,
                StateHash = stateHash,
                Nonce = blockIndex
            };

            var keyPair = _wallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode(), HardforkHeights.IsHardfork_9Active(blockIndex)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(blockIndex));

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = {_wallet.EcdsaKeyPair.PublicKey},
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _wallet.EcdsaKeyPair.PublicKey,
                        Value = headerSignature,
                    }
                }
            };
            return (header, multisig);
        }

        private OperatingError ExecuteBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            Console.WriteLine($"Executed block: {block.Header.Index}");
            return status;
        }

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc, bool useNewChainId)
        {
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var tx = new Transaction
            {
                To = to,
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(keyPair.PublicKey.GetAddress()) +
                        (ulong) nonceInc,
                Value = value
            };
            return Signer.Sign(tx, keyPair, useNewChainId);
        }
    }
}