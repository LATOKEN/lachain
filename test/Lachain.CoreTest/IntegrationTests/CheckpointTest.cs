using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Checkpoints;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Logger;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class CheckpointTest
    {
        private static readonly ILogger<CheckpointTest> Logger = LoggerFactory.GetLoggerForClass<CheckpointTest>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private ICheckpointManager _checkpointManager = null!;
        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private ISnapshotIndexRepository _snapshotIndexer = null!;
        private IConfigManager _configManager = null!;
        private IContainer? _container;

        public CheckpointTest()
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
            _checkpointManager = _container.Resolve<ICheckpointManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _snapshotIndexer = _container.Resolve<ISnapshotIndexRepository>();
            _configManager = _container.Resolve<IConfigManager>();

            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
            }
            _blockManager.TryBuildGenesisBlock();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        [Repeat(2)]
        public void Test_Checkpoint()
        {
            Assert.AreEqual(true, _checkpointManager.IsCheckpointConsistent(), "checkpoint not consistent");
            ulong totalBlocks = 10;
            GenerateBlocks(totalBlocks);
            Assert.AreEqual(true, _checkpointManager.IsCheckpointConsistent(), "checkpoint not consistent");
            var blockHeights = new List<ulong>();
            for (ulong height = 1; height <= totalBlocks; height++)
            {
                blockHeights.Add(height);
                var checkpointList = CreateCheckpointConfig(blockHeights);
                _checkpointManager.AddCheckpoints(checkpointList);
                Assert.AreNotEqual(null , _checkpointManager.CheckpointBlockHeight, "checkpoint not saved");
                Assert.AreEqual(height, _checkpointManager.CheckpointBlockHeight, "checkpoint block height mismatch");
                Assert.AreEqual(true, _checkpointManager.IsCheckpointConsistent(), "checkpoint not consistent");
            }

            var checkpoints = _checkpointManager.GetAllSavedCheckpoint();
            Assert.AreEqual(blockHeights.Count, checkpoints.Count,
                $"Tried to save {blockHeights.Count} checkpoints but saved only {checkpoints.Count} checkpoints");
        }

        [Test]
        [Repeat(2)]
        public void Test_SateHash()
        {
            ulong totalBlocks = 10;
            GenerateBlocks(totalBlocks);
            var blockHeights = new List<ulong>();
            for (ulong height = 1; height <= totalBlocks; height++)
            {
                var block = _blockManager.GetByHeight(height);
                blockHeights.Add(height);
                var checkpointList = CreateCheckpointConfig(blockHeights);
                _checkpointManager.AddCheckpoints(checkpointList);
                Assert.AreEqual(height, _checkpointManager.CheckpointBlockHeight);
                Assert.AreEqual(block!.Hash, _checkpointManager.CheckpointBlockHash);
                
                var blockchainSnapshot = _snapshotIndexer.GetSnapshotForBlock(height);
                var snapshots = blockchainSnapshot.GetAllSnapshot();
                foreach (var snapshot in snapshots)
                {
                    var repositoryType = (RepositoryType) snapshot.RepositoryId;
                    if (repositoryType ==  RepositoryType.BlockRepository) continue;
                    var hash = _checkpointManager.GetStateHashForSnapshotType(repositoryType, height);
                    if (hash is null)
                    {
                        Logger.LogInformation($"found null hash for {repositoryType} for block {height}");
                        Assert.That(false);
                    }
                    else
                    {
                        Logger.LogInformation($"state hash for block {height} and for {repositoryType}: {hash.ToHex()}");
                        CheckHex(hash.ToHex());
                        Assert.AreEqual(snapshot.Hash, hash, $"Hash mismatch for {repositoryType} for block {height}");
                    }
                }
            }
        }

        private List<CheckpointConfigInfo> CreateCheckpointConfig(List<ulong> blockHeights)
        {
            var checkpoints = new List<CheckpointConfigInfo>();
            foreach (var height in blockHeights)
            {
                checkpoints.Add(new CheckpointConfigInfo(height));
            }

            return checkpoints;
        }

        private void CheckHex(string hex)
        {
            Assert.AreNotEqual("0x", hex, "empty hash");
            Assert.AreNotEqual("", hex, "empty hash");
            Assert.AreEqual("0x" , hex.Substring(0,2), "found invalid hex");
        }

        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 0; i < blockNum; i++)
            {
                var txs = GetCurrentPoolTxs();
                var block = BuildNextBlock(txs);
                var result = ExecuteBlock(block, txs);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxs()
        {
            return _transactionPool.Peek(1000, 1000).ToArray();
        }

        private Block BuildNextBlock(TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);

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
                keyPair.PrivateKey.Encode(),
                HardforkHeights.IsHardfork_9Active(header.Index)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(header.Index));

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

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            return status;
        }

    }
}