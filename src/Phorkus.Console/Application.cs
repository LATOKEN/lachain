using System;
using System.Linq;
using System.Threading;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Consensus;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.State;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Core.Storage;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Hestia;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.RocksDB;
using Phorkus.Utility.Utils;

namespace Phorkus.Console
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;

        public Application()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, exception) =>
            {
                System.Console.Error.WriteLine(exception);
            };
            
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));

            containerBuilder.RegisterModule<LoggingModule>();
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<CryptographyModule>();
            containerBuilder.RegisterModule<MessagingModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<PersistentStorageModule>();

            _container = containerBuilder.Build();
        }

        public void Start(string[] args)
        {
            var blockchainManager = _container.Resolve<IBlockchainManager>();
            var blockchainContext = _container.Resolve<IBlockchainContext>();
            var configManager = _container.Resolve<IConfigManager>();
            var blockRepository = _container.Resolve<IBlockRepository>();
            var crypto = _container.Resolve<ICrypto>();
            var transactionFactory = _container.Resolve<ITransactionBuilder>();
            var transactionManager = _container.Resolve<ITransactionManager>();
            var blockManager = _container.Resolve<IBlockManager>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var thresholdManager = _container.Resolve<IThresholdManager>();
            var blockchainStateManager = _container.Resolve<IBlockchainStateManager>();
            
            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            
            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToHex());
            System.Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToHex());
            System.Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToArray()).ToHex());
            System.Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock(keyPair))
                System.Console.WriteLine("Generated genesis block");

            var genesisBlock = blockRepository.GetBlockByHeight(0);
            System.Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            System.Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            System.Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            System.Console.WriteLine($" + timestamp: {genesisBlock.Header.Timestamp}");
            System.Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            System.Console.WriteLine($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                System.Console.WriteLine($" + - {s.Buffer.ToHex()}");
            System.Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");
            
            var asset = blockchainStateManager.LastApprovedSnapshot.Assets.GetAssetByName("LA");
            
            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();
            
            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeaderHeight);
            System.Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeight);
            System.Console.WriteLine("-------------------------------");
//            System.Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
//            System.Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            System.Console.WriteLine("-------------------------------");
            
            transactionVerifier.Start();
            consensusManager.Start();
            blockSynchronizer.Start();
            
//            var sig = thresholdManager.SignData(keyPair, "secp256k1", "0xbadcab1e".HexToBytes());

            System.Console.CancelKeyPress += (sender, e) => _interrupt = true;
            while (!_interrupt)
                Thread.Sleep(1000);
        }

        private bool _interrupt;
    }
}