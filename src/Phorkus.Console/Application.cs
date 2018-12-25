using System;
using System.Linq;
using System.Threading;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Consensus;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.CrossChain;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;
using Phorkus.Storage.State;
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

            _container = containerBuilder.Build();
        }

        public void Start(string[] args)
        {
            var blockchainManager = _container.Resolve<IBlockchainManager>();
            var blockchainContext = _container.Resolve<IBlockchainContext>();
            var configManager = _container.Resolve<IConfigManager>();
            var blockRepository = _container.Resolve<IBlockRepository>();
            var crypto = _container.Resolve<ICrypto>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var blockchainStateManager = _container.Resolve<IBlockchainStateManager>();
            var crossChainManager = _container.Resolve<ICrossChainManager>();
            var networkManager = _container.Resolve<INetworkManager>();
            var messageHandler = _container.Resolve<IMessageHandler>();
            var withdrawalRunner = _container.Resolve<IWithdrawalRunner>();
//            var crossChain = _container.Resolve<ICrossChain>();

            var balanceRepository = blockchainStateManager.LastApprovedSnapshot.Balances;
            var assetRepository = blockchainStateManager.LastApprovedSnapshot.Assets;

            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);
            var thresholdKey = new ThresholdKey();
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
            var assetNames = assetRepository.GetAssetNames().ToArray();
            System.Console.WriteLine($" + genesis assets: {assetNames.Length}");
            foreach (var assetName in assetNames)
                System.Console.WriteLine($" + - {assetName}: {assetRepository.GetAssetByName(assetName)?.Hash}");
            
            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeaderHeight);
            System.Console.WriteLine("Current block height: " + blockchainContext.CurrentBlockHeight);
            System.Console.WriteLine("-------------------------------");

            _TraceBalances(assetRepository, balanceRepository, address1);
            System.Console.WriteLine("-------------------------------");
            _TraceBalances(assetRepository, balanceRepository, address2);
            System.Console.WriteLine("-------------------------------");

            var networkConfig = configManager.GetConfig<NetworkConfig>("network");
            //crossChain.Start(new ThresholdKey(), keyPair);
            networkManager.Start(networkConfig, keyPair, messageHandler);
            transactionVerifier.Start();
            consensusManager.Start();
            blockSynchronizer.Start();
            // withdrawalRunner.Start(thresholdKey, keyPair);

            //var thresholdManager = _container.Resolve<IThresholdManager>();
            //var sig = thresholdManager.SignData(keyPair, "secp256k1", "0xbadcab1e".HexToBytes());

            System.Console.CancelKeyPress += (sender, e) => _interrupt = true;
            while (!_interrupt)
                Thread.Sleep(1000);
        }

        private void _TraceBalances(IAssetSnapshot assetSnapshot, IBalanceSnapshot balanceSnapshot, UInt160 address)
        {
            foreach (var assetName in assetSnapshot.GetAssetNames())
            {
                var prettyName = assetName;
                if (prettyName.Length < 3)
                    prettyName = $"{prettyName} ";
                System.Console.WriteLine(
                    $"Balance of {prettyName} {address.Buffer.ToHex().Substring(0, 4)}: {balanceSnapshot.GetAvailableBalance(address, assetSnapshot.GetAssetByName(assetName)?.Hash)}");
            }
        }

        private bool _interrupt;
    }
}