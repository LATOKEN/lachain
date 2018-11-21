using System;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Newtonsoft.Json;
using Phorkus.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Consensus;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.DI;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Core.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Logger;
using Phorkus.RocksDB;

namespace Phorkus.Console
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;

        public Application()
        {
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
            var networkManager = _container.Resolve<INetworkManager>();
            var blockchainManager = _container.Resolve<IBlockchainManager>();
            var blockchainContext = _container.Resolve<IBlockchainContext>();
            var configManager = _container.Resolve<IConfigManager>();
            var blockRepository = _container.Resolve<IBlockRepository>();
            var assetRepository = _container.Resolve<IAssetRepository>();
            var crypto = _container.Resolve<ICrypto>();
            var transactionFactory = _container.Resolve<ITransactionFactory>();
            var balanceRepository = _container.Resolve<IBalanceRepository>();
            var transactionManager = _container.Resolve<ITransactionManager>();
            var blockManager = _container.Resolve<IBlockManager>();

            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);

            System.Console.WriteLine("-------------------------------");
            System.Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToByteArray().ToHex());
            System.Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToByteArray().ToHex());
            System.Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToHex());
            System.Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock(keyPair))
                System.Console.WriteLine("Generated genesis block");

            var genesisBlock = blockRepository.GetBlockByHeight(0);
            System.Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            System.Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            System.Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            System.Console.WriteLine($" + timestamp: {genesisBlock.Header.Timestamp}");
            System.Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            System.Console.WriteLine($" + transactionHashes: {genesisBlock.Header.TransactionHashes.Count}");
            foreach (var s in genesisBlock.Header.TransactionHashes)
                System.Console.WriteLine($" + - {s.Buffer.ToHex()}");
            System.Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");
            System.Console.WriteLine("-------------------------------");

            var asset = assetRepository.GetAssetByName("LA");

            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            System.Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
            System.Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            System.Console.WriteLine("-------------------------------");

            var transferTx = transactionFactory.TransferMoney(
                address1,
                address2,
                asset.Hash,
                Money.FromDecimal(1.2m)
            );
            var signed = transactionManager.Sign(transferTx, keyPair);

            var latestBlock = blockchainContext.CurrentBlock;
            var blockWithTxs =
                new BlockBuilder(new[] {signed}, latestBlock.Hash, latestBlock.Header.Index).Build(123456);
            var block = blockWithTxs.Block;
            block.Multisig = new MultiSig
            {
                Quorum = 1,
                Validators =
                {
                    keyPair.PublicKey
                }
            };
            block.Multisig.Signatures.Add(new MultiSig.Types.SignatureByValidator
            {
                Key = keyPair.PublicKey,
                Value = blockManager.Sign(block.Header, keyPair)
            });
            blockchainManager.PersistBlockManually(block, blockWithTxs.Transactions);
            
            System.Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
            System.Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            System.Console.WriteLine("-------------------------------");

            networkManager.Start();
            
            Thread.Sleep(1000);
            var consensusManager = _container.Resolve<IConsensusManager>();
            consensusManager.Start();
            

            System.Console.CancelKeyPress += (sender, e) => _interrupt = true;
            while (!_interrupt)
                Thread.Sleep(1000);
        }

        private bool _interrupt;
    }
}