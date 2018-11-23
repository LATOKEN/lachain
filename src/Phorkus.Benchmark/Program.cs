using System;
using System.Threading;
using Phorkus.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Consensus;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Config;
using Phorkus.Core.Cryptography;
using Phorkus.Core.DI;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Network;
using Phorkus.Proto;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Logger;
using Phorkus.RocksDB;

namespace Phorkus.Benchmark
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;
        
        internal static void Main(string[] args)
        {
            var app = new Application();
            app.Start(args);
        }

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

            Console.WriteLine("-------------------------------");
            Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToByteArray().ToHex());
            Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToByteArray().ToHex());
            Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToHex());
            Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock(keyPair))
                Console.WriteLine("Generated genesis block");

            var genesisBlock = blockRepository.GetBlockByHeight(0);
            Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            Console.WriteLine($" + timestamp: {genesisBlock.Header.Timestamp}");
            Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            Console.WriteLine($" + transactionHashes: {genesisBlock.Header.TransactionHashes.Count}");
            foreach (var s in genesisBlock.Header.TransactionHashes)
                Console.WriteLine($" + - {s.Buffer.ToHex()}");
            Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");
            Console.WriteLine("-------------------------------");

            var asset = assetRepository.GetAssetByName("LA");

            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeaderHeight);
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeight);
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
            Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            Console.WriteLine("-------------------------------");

            _BenchTxProcessing(transactionFactory, blockchainContext, transactionManager, blockManager,
                blockchainManager, keyPair, asset);

            _BenchOneTxInBlock(transactionFactory, blockchainContext, transactionManager, blockManager,
                blockchainManager, keyPair, asset);


            Console.WriteLine("-------------------------------");
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeaderHeight);
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeight);
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
            Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            Console.WriteLine("-------------------------------");
        }

        private static void _Benchmark(string text, Func<int, int> action, uint tries)
        {
            var lastTime = TimeUtils.CurrentTimeMillis();
            var mod = tries / 100;
            if (mod == 0)
                mod = 1;
            for (var i = 0; i < tries; i++)
            {
                if (i % mod == 0)
                {
                    Console.CursorLeft = 0;
                    Console.Write($"{text} {100 * i / tries}%");
                }

                action(i);
            }
            
            var deltaTime = TimeUtils.CurrentTimeMillis() - lastTime;
            Console.CursorLeft = text.Length;
            Console.WriteLine($"{1000.0 * tries / deltaTime} TPS");
        }

        private static void _BenchTxProcessing(
            ITransactionFactory transactionFactory,
            IBlockchainContext blockchainContext,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainManager blockchainManager,
            KeyPair keyPair,
            Asset asset)
        {
            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            var transactionPool = new TransactionPool(null,
                transactionManager);

            const int txGenerate = 1000;
            const int txPerBlock = 500;
            
            _Benchmark("Building TX pool... ", i =>
            {
                var tx = transactionFactory.TransferTransaction(address1, address2, asset.Hash, Money.FromDecimal(1.2m));
                tx.Nonce += (ulong) i;
                transactionPool.Add(transactionManager.Sign(tx, keyPair));
                return i;
            }, txGenerate);

            var blocks = new BlockWithTransactions[transactionPool.Size() / txPerBlock];
            
            _Benchmark("Generating blocks... ", i =>
            {
                var txs = transactionPool.Peek(txPerBlock);
                var latestBlock = blockchainContext.CurrentBlock;
                if (i > 0)
                    latestBlock = blocks[i - 1].Block;
                var blockWithTxs =
                    new BlockBuilder(txs, latestBlock.Hash, latestBlock.Header.Index).Build(123456);
                var block = blockWithTxs.Block;
                block.Multisig = new MultiSig
                {
                    Quorum = 1,
                    Signatures =
                    {
                        new MultiSig.Types.SignatureByValidator
                        {
                            Key = keyPair.PublicKey,
                            Value = blockManager.Sign(block.Header, keyPair)
                        }
                    },
                    Validators = {keyPair.PublicKey}
                };
                blocks[i] = blockWithTxs;
                return i;
            }, transactionPool.Size() / txPerBlock);
            
            _Benchmark("Processing blocks... ", i =>
            {
                var blockWithTxs = blocks[i];
                blockchainManager.PersistBlockManually(blockWithTxs.Block, blockWithTxs.Transactions);
                return i;
            }, (uint) blocks.Length);
        }

        private static void _BenchOneTxInBlock(
            ITransactionFactory transactionFactory,
            IBlockchainContext blockchainContext,
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainManager blockchainManager,
            KeyPair keyPair,
            Asset asset)
        {
            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();
            var lastTime = TimeUtils.CurrentTimeMillis();
            const int tries = 1000;
            for (var i = 0; i < tries; i++)
            {
                if (i % 10 == 0)
                {
                    Console.CursorLeft = 0;
                    Console.Write($"Benchmarking... {100 * i / tries}%");
                }

                var transferTx =
                    transactionFactory.TransferTransaction(address1, address2, asset.Hash, Money.FromDecimal(1.2m));
                var signed = transactionManager.Sign(transferTx, keyPair);
                var latestBlock = blockchainContext.CurrentBlock;
                var blockWithTxs =
                    new BlockBuilder(new[] {signed}, latestBlock.Hash, latestBlock.Header.Index).Build(123456);
                var block = blockWithTxs.Block;
                block.Multisig = new MultiSig
                {
                    Quorum = 1,
                    Signatures =
                    {
                        new MultiSig.Types.SignatureByValidator
                        {
                            Key = keyPair.PublicKey,
                            Value = blockManager.Sign(block.Header, keyPair)
                        }
                    },
                    Validators = {keyPair.PublicKey}
                };
                blockchainManager.PersistBlockManually(block, blockWithTxs.Transactions);
            }

            var deltaTime = TimeUtils.CurrentTimeMillis() - lastTime;
            Console.CursorLeft = "Benchmarking... ".Length;
            Console.WriteLine($"{1000 * tries / deltaTime} TPS");
        }
    }
}