using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using NLog;
using RocksDbSharp;

namespace Lachain.LightSyncDemo
{
    public class Application : IBootstrapper, IDisposable
    {
        private readonly IContainer _container1;
        private readonly IContainer _container2;
        private static readonly ILogger<Application> Logger = LoggerFactory.GetLoggerForClass<Application>();

        private IContainer CreateContainer(string dataDir, RunOptions options)
        {
            options.DataDir = dataDir;
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(options.ConfigPath, options));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            return containerBuilder.Build();
        }

        public Application(RunOptions options)
        {
            var logLevel = options.LogLevel ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (logLevel != null) logLevel = char.ToUpper(logLevel[0]) + logLevel.ToLower().Substring(1);
            if (!new[] {"Trace", "Debug", "Info", "Warn", "Error", "Fatal"}.Contains(logLevel))
                logLevel = "Info";
            LogManager.Configuration.Variables["consoleLogLevel"] = logLevel;
            LogManager.ReconfigExistingLoggers();
            _container1 = CreateContainer("ChainLachain1", options);
            _container2 = CreateContainer("ChainLachain2", options);
        }

        private void StartContainer(string id, IContainer container)
        {
            var blockManager = container.Resolve<IBlockManager>();
            var transactionVerifier = container.Resolve<ITransactionVerifier>();
            var stateManager = container.Resolve<IStateManager>();
            var wallet = container.Resolve<IPrivateWallet>();
            var localTransactionRepository = container.Resolve<ILocalTransactionRepository>();

            localTransactionRepository.SetWatchAddress(wallet.EcdsaKeyPair.PublicKey.GetAddress());

            if (blockManager.TryBuildGenesisBlock())
                Logger.LogInformation($"{id}: Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0)
                               ?? throw new Exception("Genesis block was not persisted");
            Logger.LogInformation($"{id}: Genesis Block: " + genesisBlock.Hash.ToHex());
            Logger.LogInformation($"{id}: + prevBlockHash: {genesisBlock.Header.PrevBlockHash.ToHex()}");
            Logger.LogInformation($"{id}: + merkleRoot: {genesisBlock.Header.MerkleRoot.ToHex()}");
            Logger.LogInformation($"{id}: + nonce: {genesisBlock.Header.Nonce}");
            Logger.LogInformation($"{id}: + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                Logger.LogInformation($" +{id}: - {s.ToHex()}");
            Logger.LogInformation($"{id}: + hash: {genesisBlock.Hash.ToHex()}");

            Logger.LogInformation($"{id}: Current block height: " + blockManager.GetHeight());
            Logger.LogInformation($"{id}: Node public key: {wallet.EcdsaKeyPair.PublicKey.EncodeCompressed().ToHex()}");
            Logger.LogInformation($"{id}: Node address: {wallet.EcdsaKeyPair.PublicKey.GetAddress().ToHex()}");

            transactionVerifier.Start();
        }

        public void Start(RunOptions options)
        {
            // StartContainer("ChainLachain1", _container1);
            // StartContainer("ChainLachain2", _container2);

            /**
            Instantiate classed for DB-1
            */
            // var stateManager1 = _container1.Resolve<IStateManager>();
            // var blockManager1 = _container1.Resolve<IBlockManager>();
            // var storageManager1 = _container1.Resolve<IStorageManager>();
            // var rocksDbContext1 = _container1.Resolve<IRocksDbContext>();

            // var nodeRepository1 = new NodeRepository(rocksDbContext1);
            // var versionRepository1 = new VersionRepository(rocksDbContext1);

            // /**
            // Instantiate classed for DB-2
            // */
            // var stateManager2 = _container2.Resolve<IStateManager>();
            // var blockManager2 = _container2.Resolve<IBlockManager>();
            // var storageManager2 = _container2.Resolve<IStorageManager>();
            // var rocksDbContext2 = _container2.Resolve<IRocksDbContext>();
            
            // var nodeRepository2 = new NodeRepository(rocksDbContext2);
            // var versionRepository2 = new VersionRepository(rocksDbContext2);
            
            // RocksDbAtomicWrite rocksDbAtomicWrite2 = new RocksDbAtomicWrite(rocksDbContext2);
            
            // Logger.LogInformation($"DB-1 :: Block Repo {versionRepository1.GetVersion(7).ToString()}");
            // Logger.LogInformation($"DB-2 :: Block Repo {versionRepository2.GetVersion(7).ToString()}");
            // Logger.LogInformation($"DB-1 :: Balance Repo {versionRepository1.GetVersion(1).ToString()}");
            // Logger.LogInformation($"DB-2 :: Balance Repo {versionRepository2.GetVersion(1).ToString()}");
            // Logger.LogInformation($"DB-1 :: Storage Repo {versionRepository1.GetVersion(5).ToString()}");
            // Logger.LogInformation($"DB-2 :: Storage Repo {versionRepository2.GetVersion(5).ToString()}");


            // /**
            // Capture the root nodes for various trees in DB-1
            // */
            // var balancesVersion1 = stateManager1.CurrentSnapshot.Balances.Version;
            // var blockVersion1 = stateManager1.CurrentSnapshot.Blocks.Version;
            // var storageVersion1 = stateManager1.CurrentSnapshot.Storage.Version;

            /**
            Add new block to DB-1
            */
            var block = BuildNextBlock(_container1);
            var result = ExecuteBlock(_container1, block);

            
            // var height1 = blockManager1.GetHeight();
            // Logger.LogInformation($"Container_1 Block Height = {height1}");
            // Logger.LogInformation($"BalanceVersion_1 = {balancesVersion1}");
            // Logger.LogInformation($"BlockVersion_1 = {blockVersion1}");
            // Logger.LogInformation($"StorageVersion_1 = {storageVersion1}");

            // var versionFactory1 = new VersionFactory(versionRepository1.GetVersion(7));
            // var trieHashMap1 = new TrieHashMap(nodeRepository1, versionFactory1);

            // /**
            // Get the Node Values & Node Ids, for DB-1
            // */
            // var nodeValues = trieHashMap1.GetSerializedNodes(blockVersion1);
            // var nodeIds = trieHashMap1.GetNodeIds(blockVersion1);

            // /**
            // Start writing to new DB
            // */
            // var nodeValIterator = nodeValues.GetEnumerator();
            // var nodeIdIterator = nodeIds.GetEnumerator();

            // Logger.LogInformation($"Total Nodes {nodeIds.Count().ToString()}");

            // while(nodeIdIterator.MoveNext() && nodeValIterator.MoveNext())
            // {
            //     Logger.LogInformation($"ID = {nodeIdIterator.Current}");
            //     // Logger.LogInformation($"Val = {String.Join(" ",nodeValIterator.Current)}");
            //     nodeRepository2.WriteNodeToBatch(nodeIdIterator.Current, NodeSerializer.FromBytes(nodeValIterator.Current), rocksDbAtomicWrite2);
            // }

            // var writeBatch2 = rocksDbAtomicWrite2.GetWriteBatch();
            // nodeRepository2.SaveBatch(writeBatch2);


            // // versionRepository2.SetVersion
            // var versionFactory2 = new VersionFactory(versionRepository2.GetVersion(7));
            // var trieHashMap2 = new TrieHashMap(nodeRepository2, versionFactory2);

            // var repositoryManager2 = new RepositoryManager(7, rocksDbContext2, versionFactory2, versionRepository2);
            // stateManager2.CurrentSnapshot.Blocks.Version = versionRepository1.GetVersion(7);
            // stateManager2.CurrentSnapshot.Balances.Commit();
            // stateManager2.Commit();
            
            // Logger.LogInformation($"Version Repo {stateManager2.CurrentSnapshot.Blocks.Version.ToString()}");

            // /**
            // Capture the root nodes for various trees in DB-2
            // */
            // var balancesVersion2 = stateManager2.CurrentSnapshot.Balances.Version;
            // var blockVersion2 = stateManager2.CurrentSnapshot.Blocks.Version;
            // var storageVersion2 = stateManager2.CurrentSnapshot.Storage.Version;
            // var height2 = blockManager2.GetHeight();

            // Logger.LogInformation($"Container_2 Block Height = {height2}");
            // Logger.LogInformation($"BalanceVersion_2 = {balancesVersion2}");
            // Logger.LogInformation($"BlockVersion_2 = {blockVersion2}");
            // Logger.LogInformation($"StorageVersion_2 = {storageVersion2}");

            // var nodeValues2 = trieHashMap2.GetSerializedNodes(blockVersion2);
            // var nodeIds2 = trieHashMap2.GetNodeIds(blockVersion2);

            // foreach (var id in nodeIds2)
            // {
            //     Logger.LogDebug($"Id {id}");
            // }
            
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("Interrupt received. Exiting...");
                _interrupt = true;
                Dispose();
            };

            while (!_interrupt)
                Thread.Sleep(1000);
        }


        private Block BuildNextBlock(IContainer container, TransactionReceipt[] receipts = null)
        {
            var _stateManager = container.Resolve<IStateManager>();
            receipts ??= new TransactionReceipt[] { };

            var merkleRoot = UInt256Utils.Zero;

            if (receipts.Any())
                merkleRoot = MerkleTree.ComputeRoot(receipts.Select(tx => tx.Hash).ToArray()) ??
                             throw new InvalidOperationException();

            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) =
                BuildHeaderAndMultisig(container, merkleRoot, predecessor, _stateManager.LastApprovedSnapshot.StateHash);

            return new Block
            {
                Header = header,
                Hash = header.Keccak(),
                Multisig = multisig,
                TransactionHashes = {receipts.Select(tx => tx.Hash)},
            };
        }

        private (BlockHeader, MultiSig) BuildHeaderAndMultisig(IContainer container, UInt256 merkleRoot, Block? predecessor,
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

            var _wallet = container.Resolve<IPrivateWallet>();
            var keyPair = _wallet.EcdsaKeyPair;
            var Crypto = CryptoProvider.GetCrypto();
            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

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

        private OperatingError ExecuteBlock(IContainer container, Block block, TransactionReceipt[] receipts = null)
        {
            var _stateManager = container.Resolve<IStateManager>();
            var _blockManager = container.Resolve<IBlockManager>();

            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(_stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight());
            var (header, multisig) = BuildHeaderAndMultisig(container, block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            Console.WriteLine($"Executed block: {block.Header.Index}");
            return status;
        }

        private bool _interrupt;

        public void Dispose()
        {
            _container1.Dispose();
            _container2.Dispose();
        }
    }
}