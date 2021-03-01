using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using SystemException = System.SystemException;

namespace Lachain.CoreTest.RPC.HTTP
{
    public class FastSyncServiceTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private IBlockManager _blockManager1 = null!;
        private IContainer _container1 = null!;
        private ITransactionPool _transactionPool1 = null!;
        private IStateManager _stateManager1 = null!;
        private IPrivateWallet _wallet1 = null!;
        private VersionRepository _versionRepository1 = null!;
        private IRocksDbContext _rocksDbContext1 = null!;
        private NodeRepository _nodeRepository1 = null!;


        private IBlockManager _blockManager2 = null!;
        private IContainer _container2 = null!;
        private IBlockSynchronizer _blockSynchronizer2 = null!;
        private VersionRepository _versionRepository2 = null!;
        private IRocksDbContext _rocksDbContext2 = null!;
        private NodeRepository _nodeRepository2 = null!;
        private IStateManager _stateManager2 = null!;


        private Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>> _repoBlocks =
            new Dictionary<ulong, Tuple<List<ulong>, List<byte[]>>>();

        Dictionary<ulong, List<ulong>> _blockVersion = new Dictionary<ulong, List<ulong>>();

        private readonly List<ulong> _repoList = new List<ulong>()
        {
            1, 4, 5, 6, 7, 9, 10
        };

        [SetUp]
        public void Set_Base_Container()
        {
            Set_Base_Container("config.json");
            Set_Empty_Container("config2.json");
        }

        [Test]
        public void Test_Container1_InitialState()
        {
            Assert.AreEqual(10, _blockManager1.GetHeight());
        }

        [Test]
        public void Test_Container2_InitialState()
        {
            Console.WriteLine(_blockManager2.GetHeight());
            Assert.AreEqual(0, _blockManager2.GetHeight());
        }

        [Test]
        public void Test_Perform_Sync()
        {
            ulong version = 0;
            TrieHashMap trieHashMap1 = null!;

            foreach (var repoType in _repoList)
            {
                switch (repoType)
                {
                    case 1:
                        version = _stateManager1.CurrentSnapshot.Balances.Version;
                        break;
                    case 4:
                        version = _stateManager1.CurrentSnapshot.Contracts.Version;
                        break;
                    case 5:
                        version = _stateManager1.CurrentSnapshot.Storage.Version;
                        break;
                    case 6:
                        version = _stateManager1.CurrentSnapshot.Transactions.Version;
                        break;
                    case 7:
                        version = _stateManager1.CurrentSnapshot.Blocks.Version;
                        break;
                    case 9:
                        version = _stateManager1.CurrentSnapshot.Events.Version;
                        break;
                    case 10:
                        version = _stateManager1.CurrentSnapshot.Validators.Version;
                        break;
                    default:
                        break;
                }

                var versionFactory1 = new VersionFactory(_versionRepository1.GetVersion(Convert.ToUInt32(repoType)));
                trieHashMap1 = new TrieHashMap(_nodeRepository1, versionFactory1);

                if (trieHashMap1.GetNodeIds(version).ToList().Count > 0)
                {
                    var t = Tuple.Create(trieHashMap1.GetNodeIds(version).ToList(),
                        trieHashMap1.GetSerializedNodes(version).ToList());

                    _repoBlocks.Add(repoType, t);
                }
            }

            _blockSynchronizer2.SetNodeForPersist(_repoBlocks);

            RocksDbAtomicWrite rocksDbAtomicWrite = new RocksDbAtomicWrite(_rocksDbContext2);
            _blockSynchronizer2.PersistNodesForFastSync(_nodeRepository2, rocksDbAtomicWrite);

            var metaVersionFactory = new VersionFactory(_versionRepository1.GetVersion(0));
            _blockSynchronizer2.SetMetaVersion(_versionRepository2, rocksDbAtomicWrite,
                metaVersionFactory.CurrentVersion);

            Assert.AreEqual(_blockManager2.GetHeight(), _blockManager1.GetHeight(), "Height MisMatch");

            Assert.AreEqual(_stateManager2.CurrentSnapshot.StateHash, _stateManager1.CurrentSnapshot.StateHash,
                "StateHash MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Balances.Version,
                _stateManager1.CurrentSnapshot.Balances.Version,
                "Balance Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Contracts.Version,
                _stateManager1.CurrentSnapshot.Contracts.Version, "Contracts Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Storage.Version,
                _stateManager1.CurrentSnapshot.Storage.Version, "Storage Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Transactions.Version,
                _stateManager1.CurrentSnapshot.Transactions.Version, "Transactions Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Blocks.Version,
                _stateManager1.CurrentSnapshot.Blocks.Version, "Blocks Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Events.Version,
                _stateManager1.CurrentSnapshot.Events.Version, "Events Version MisMatch");
            Assert.AreEqual(_stateManager2.CurrentSnapshot.Validators.Version,
                _stateManager1.CurrentSnapshot.Validators.Version, "Validators Version MisMatch");

            Assert.AreEqual(_stateManager2.LastApprovedSnapshot.StateHash,
                _stateManager1.LastApprovedSnapshot.StateHash, "LastApprovedSnapshot StateHash MisMatch");
        }
        
        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container1?.Dispose();
            _container2?.Dispose();
        }

        private void Set_Base_Container(string configName)
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), configName),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<RpcModule>();
            containerBuilder.RegisterModule<ConsoleModule>();

            _container1 = containerBuilder.Build();
            _blockManager1 = _container1.Resolve<IBlockManager>();
            _stateManager1 = _container1.Resolve<IStateManager>();
            _wallet1 = _container1.Resolve<IPrivateWallet>();
            _transactionPool1 = _container1.Resolve<ITransactionPool>();

            TestUtils.DeleteTestChainData();

            _blockManager1.TryBuildGenesisBlock();

            for (int i = 0; i < 10; i++)
            {
                var block = BuildNextBlock(_wallet1);

                var result = ExecuteBlock(block, _wallet1);
                Assert.AreEqual(OperatingError.Ok, result);
            }

            _rocksDbContext1 = _container1.Resolve<IRocksDbContext>();
            _nodeRepository1 = new NodeRepository(_rocksDbContext1);
            _versionRepository1 = new VersionRepository(_rocksDbContext1);
        }

        private void Set_Empty_Container(string configName)
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), configName),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<RpcModule>();
            containerBuilder.RegisterModule<ConsoleModule>();

            _container2 = containerBuilder.Build();
            _blockManager2 = _container2.Resolve<IBlockManager>();
            _blockSynchronizer2 = _container2.Resolve<IBlockSynchronizer>();
            _rocksDbContext2 = _container2.Resolve<IRocksDbContext>();
            _stateManager2 = _container2.Resolve<IStateManager>();

            _nodeRepository2 = new NodeRepository(_rocksDbContext2);
            _versionRepository2 = new VersionRepository(_rocksDbContext2);

            _blockManager2.TryBuildGenesisBlock();
        }

        private Block BuildNextBlock(IPrivateWallet wallet, TransactionReceipt[] receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager1.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager1.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(merkleRoot, predecessor, _stateManager1.LastApprovedSnapshot.StateHash, wallet);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = {receipts.Select(tx => tx.Hash)},
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(UInt256 merkleRoot, Block? predecessor,
            UInt256 stateHash, IPrivateWallet wallet)
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

            var keyPair = wallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = {wallet.EcdsaKeyPair.PublicKey},
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = wallet.EcdsaKeyPair.PublicKey,
                        Value = headerSignature,
                    }
                }
            };
            return (header, multisig);
        }

        private OperatingError ExecuteBlock(Block block, IPrivateWallet wallet, TransactionReceipt[] receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager1.Emulate(block, receipts);

            var predecessor =
                _stateManager1.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager1.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash, wallet);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager1.Execute(block, receipts, true, true);
            return status;
        }
    }
}