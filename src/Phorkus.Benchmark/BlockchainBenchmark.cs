using System;
using System.Collections.Generic;
using System.IO;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace Phorkus.Benchmark
{
    public class BlockchainBenchmark : IBootstrapper
    {
        private readonly IContainer _container;

        public BlockchainBenchmark()
        {
            if (Directory.Exists("ChainPhorkus"))
                Directory.Delete("ChainPhorkus", true);
            
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
            var crypto = _container.Resolve<ICrypto>();
            var transactionBuilder = _container.Resolve<ITransactionBuilder>();
            var transactionManager = _container.Resolve<ITransactionManager>();
            var blockManager = _container.Resolve<IBlockManager>();
            var stateManager = _container.Resolve<IStateManager>();
            
            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);

            Console.WriteLine("-------------------------------");
            Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToByteArray().ToHex());
            Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToByteArray().ToHex());
            Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToHex());
            Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock())
                Console.WriteLine("Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0);
            Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            Console.WriteLine($" + timestamp: {genesisBlock.Header.Timestamp}");
            Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            Console.WriteLine($" + transactionHashes: {genesisBlock.TransactionHashes.Count}");
            foreach (var s in genesisBlock.TransactionHashes)
                Console.WriteLine($" + - {s.Buffer.ToHex()}");
            Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");

            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            Console.WriteLine("-------------------------------");
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeight);
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Balance of LA 0x3e: " + stateManager.LastApprovedSnapshot.Balances.GetBalance(address1));
            Console.WriteLine("Balance of LA 0x6b: " + stateManager.LastApprovedSnapshot.Balances.GetBalance(address2));
            Console.WriteLine("-------------------------------");

            _BenchTxProcessing(transactionBuilder, blockchainContext, transactionManager, blockManager,
                blockchainManager, keyPair);
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
            Console.WriteLine($"{1000.0 * tries / deltaTime} RPS");
        }

        private void _BenchTxProcessing(
            ITransactionBuilder transactionBuilder,
            IBlockchainContext blockchainContext,
            ITransactionSigner transactionSigner,
            IBlockManager blockManager,
            IBlockchainManager blockchainManager,
            KeyPair keyPair)
        {
            var address1 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();
            var address2 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();

            var transactionPool = _container.Resolve<ITransactionPool>();

            const int txGenerate = 2000;
            const int txPerBlock = 1000;

            _Benchmark("Building TX pool... ", i =>
            {
                var tx = transactionBuilder.TransferTransaction(address1, address2, Money.FromDecimal(1.2m));
                tx.Nonce += (ulong) i;
                var error = transactionPool.Add(transactionSigner.Sign(tx, keyPair));
                if (error != OperatingError.Ok)
                    throw new Exception("Unable to add transcation to pool (" + error + ")");
                return i;
            }, txGenerate);

            var blocks = new BlockWithTransactions[transactionPool.Size() / txPerBlock];
            
            _Benchmark("Generating blocks... ", i =>
            {
                var txs = transactionPool.Peek(txPerBlock);
                var latestBlock = blockchainContext.CurrentBlock;
                if (i > 0)
                    latestBlock = blocks[i - 1].Block;
                var blockWithTxs = new BlockBuilder(latestBlock.Header, keyPair.PublicKey)
                    .WithTransactions(txs)
                    .Build(123456);
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

            var currentTime = TimeUtils.CurrentTimeMillis();
            
            _Benchmark("Processing blocks... ", i =>
            {
                var blockWithTxs = blocks[i];
                blockchainManager.PersistBlockManually(blockWithTxs.Block, blockWithTxs.Transactions);
                return i;
            }, (uint) blocks.Length);
            
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine($"Transaction processing... {1000.0 * txGenerate / elapsedTime} TPS");
        }

        private static void _BenchOneTxInBlock(
            ITransactionBuilder transactionBuilder,
            IBlockchainContext blockchainContext,
            ITransactionSigner transactionSigner,
            IBlockManager blockManager,
            IBlockchainManager blockchainManager,
            KeyPair keyPair)
        {
            var address1 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();
            var address2 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var lastTime = TimeUtils.CurrentTimeMillis();
            const int tries = 100;
            for (var i = 0; i < tries; i++)
            {
                if (i % 10 == 0)
                {
                    Console.CursorLeft = 0;
                    Console.Write($"Benchmarking... {100 * i / tries}%");
                }

                var transferTx =
                    transactionBuilder.TransferTransaction(address1, address2, Money.FromDecimal(1.2m));
                var signed = transactionSigner.Sign(transferTx, keyPair);
                var latestBlock = blockchainContext.CurrentBlock;
                var blockWithTxs = new BlockBuilder(latestBlock.Header, keyPair.PublicKey)
                    .WithTransactions((ICollection<TransactionReceipt>) new[] {signed})
                    .Build(123456);
                var stateHash = blockchainManager.CalcStateHash(blockWithTxs.Block, blockWithTxs.Transactions);
                var block = blockWithTxs.Block;
                block.Header.StateHash = stateHash;
                block.Hash = block.Header.ToHash256();
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
            Console.WriteLine($"{1000 * tries / deltaTime} RPS");
        }
    }
}