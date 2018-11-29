using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Config;
using Phorkus.Core.Consensus;
using Phorkus.Core.DI;
using Phorkus.Core.Network;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Faker
{
    public class BlockGenerator
    {
        private uint _blockInterval;
        private uint _blockTxs;

        public BlockGenerator(uint blockInterval, uint blockTxs)
        {
            _blockInterval = blockInterval;
            _blockTxs = blockTxs;
        }

        public void Start(IContainer container)
        {
            var blockchainManager = container.Resolve<IBlockchainManager>();
            var blockchainContext = container.Resolve<IBlockchainContext>();
            var configManager = container.Resolve<IConfigManager>();
            var blockRepository = container.Resolve<IBlockRepository>();
            var assetRepository = container.Resolve<IAssetRepository>();
            var crypto = container.Resolve<ICrypto>();
            var transactionFactory = container.Resolve<ITransactionFactory>();
            var balanceRepository = container.Resolve<IBalanceRepository>();
            var transactionManager = container.Resolve<ITransactionManager>();
            var blockManager = container.Resolve<IBlockManager>();
            var networkManager = container.Resolve<INetworkManager>();

            var consensusConfig = configManager.GetConfig<ConsensusConfig>("consensus");
            var keyPair = new KeyPair(consensusConfig.PrivateKey.HexToBytes().ToPrivateKey(), crypto);

            Console.WriteLine("-------------------------------");
            Console.WriteLine("Private Key: " + keyPair.PrivateKey.Buffer.ToHex());
            Console.WriteLine("Public Key: " + keyPair.PublicKey.Buffer.ToHex());
            Console.WriteLine(
                "Address: " + crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToArray()).ToHex());
            Console.WriteLine("-------------------------------");

            if (blockchainManager.TryBuildGenesisBlock(keyPair))
                Console.WriteLine("Generated genesis block");

            var genesisBlock = blockRepository.GetBlockByHeight(0);
            Console.WriteLine("Genesis Block: " + genesisBlock.Hash.Buffer.ToHex());
            Console.WriteLine($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.Buffer.ToHex()}");
            Console.WriteLine($" + merkleRoot: {genesisBlock.Header.MerkleRoot.Buffer.ToHex()}");
            Console.WriteLine($" + timestamp: {genesisBlock.Header.Timestamp}");
            Console.WriteLine($" + nonce: {genesisBlock.Header.Nonce}");
            Console.WriteLine($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes.ToArray())
                Console.WriteLine($" + - {s.Buffer.ToHex()}");
            Console.WriteLine($" + hash: {genesisBlock.Hash.Buffer.ToHex()}");

            var asset = assetRepository.GetAssetByName("LA");

            var address1 = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToUInt160();
            var address2 = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160();

            Console.WriteLine("-------------------------------");
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeaderHeight);
            Console.WriteLine("Current block header height: " + blockchainContext.CurrentBlockHeight);
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Balance of LA 0x3e: " + balanceRepository.GetBalance(address1, asset.Hash));
            Console.WriteLine("Balance of LA 0x6b: " + balanceRepository.GetBalance(address2, asset.Hash));
            Console.WriteLine("-------------------------------");
            Console.WriteLine("Block generation interval: " + _blockInterval);
            Console.WriteLine("Txs in block: " + _blockTxs);
            Console.WriteLine("-------------------------------");

            var from = crypto.ComputeAddress(keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            
            var validators = new[]
            {
                keyPair.PublicKey
            };
            
            networkManager.Start();
            
            Console.CancelKeyPress += (sender, e) => _interrupt = true;
            while (!_interrupt)
            {
                var time = TimeUtils.CurrentTimeMillis();
                
                var lastNonce = transactionManager.CalcNextTxNonce(from);
                
                var unsignedTxs = new List<Transaction>();
                for (var i = 0; i < _blockTxs; i++)
                {
                    var tx = transactionFactory.TransferTransaction(from, address1, asset.Hash, Money.Zero);
                    tx.Nonce = (ulong) (lastNonce + i);
                    unsignedTxs.Add(tx);
                }
                var txs = unsignedTxs.AsParallel().Select(tx => transactionManager.Sign(tx, keyPair)).ToList();
                
                var builder = new BlockBuilder(blockchainContext.CurrentBlockHeader.Header)
                    .WithMultisig(validators)
                    .WithTransactions(txs);
                var block = builder.Build(0);
                
                blockManager.Sign(block.Block.Header, keyPair);
                blockchainManager.PersistBlockManually(block.Block, block.Transactions);
                
                var delta = TimeUtils.CurrentTimeMillis() - time;
                var toSleep = _blockInterval - (long) delta;
                if (toSleep <= 0)
                    continue;
                Thread.Sleep((int) toSleep);
            }
        }
        
        private bool _interrupt;
    }
}