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
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class BlocksTest
    {
        private static readonly ILogger<BlocksTest> Logger = LoggerFactory.GetLoggerForClass<BlocksTest>();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private ITransactionVerifier _txVerifier = null!;
        private IContainer? _container;

        public BlocksTest()
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
            _txVerifier = _container.Resolve<ITransactionVerifier>();

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
        public void Test_Genesis()
        {
            Console.WriteLine( Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"));
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"),
                new RunOptions()
            ));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            using var container = containerBuilder.Build();

            var blockManager = container.Resolve<IBlockManager>();
            var stateManager = container.Resolve<IStateManager>();
            Assert.IsTrue(blockManager.TryBuildGenesisBlock());
            var genesis =
                stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(stateManager.LastApprovedSnapshot.Blocks
                    .GetTotalBlockHeight()) ?? throw new Exception();
            Console.WriteLine(
                stateManager.LastApprovedSnapshot.Balances
                    .GetBalance("0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToUInt160())
            );
            Assert.AreEqual(0, genesis.Header.Index);
            Assert.AreEqual(
                "0x0000000000000000000000000000000000000000000000000000000000000000".HexToUInt256(),
                genesis.Header.PrevBlockHash
            );
            Console.WriteLine(
                genesis.Header.StateHash.ToHex()
            );
            Assert.AreEqual(
                "0xe1cc48aee243f602ec9ab6247a8bfe082b96d2a8c963497543c9ddc4c8029789",
                genesis.Header.StateHash.ToHex()
            );
            Assert.AreEqual(0, genesis.GasPrice);
            Assert.AreEqual(null, genesis.Multisig);
            Assert.AreEqual(0, genesis.Timestamp);
        }

        [Test]
        [Repeat(2)]
        public void Test_Block_Emulation()
        {
            _blockManager.TryBuildGenesisBlock();
            var block = BuildNextBlock();
            Assert.AreEqual(OperatingError.Ok, EmulateBlock(block));
        }

        [Test]
        [Repeat(2)]
        public void Test_Block_Execution()
        {
            _blockManager.TryBuildGenesisBlock();
            var block = BuildNextBlock();
            var result = ExecuteBlock(block);
            Assert.AreEqual(OperatingError.Ok, result);

            // using new chain id
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()));
            while(!HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()))
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }

            int total = 100;
            for (var it = 0 ; it < total ; it++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        [Test]
        [Repeat(5)]
        public void Test_Block_With_Txs_Execution()
        {
            _blockManager.TryBuildGenesisBlock();
            var topUpReceipts = new List<TransactionReceipt>();
            var randomReceipts = new List<TransactionReceipt>();
            var txCount = 50;

            var coverTxFeeAmount = Money.Parse("10.0");
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(2));
                randomReceipts.Add(tx);
                topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                    (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i, 
                    HardforkHeights.IsHardfork_9Active(1)));
            }

            ExecuteTxesInSeveralBlocks(topUpReceipts);
            ExecuteTxesInSeveralBlocks(randomReceipts);

            // building random txes for new chain id. will send TopUpTx right now.
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()));
            txCount = 50;
            topUpReceipts = new List<TransactionReceipt>();
            randomReceipts = new List<TransactionReceipt>();
            coverTxFeeAmount = Money.Parse("0.0000000001");
            Console.WriteLine(Money.Wei.ToString());
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetCustomTransaction("0", Money.Wei.ToString() , true);
                randomReceipts.Add(tx);
                topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                    (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i, 
                    HardforkHeights.IsHardfork_9Active(3)));
            }
            ExecuteTxesInSeveralBlocks(topUpReceipts);

            while (!HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()))
            {
                var block = BuildNextBlock();
                var result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            ExecuteTxesInSeveralBlocks(randomReceipts);
        }

        [Test]
        public void Test_Storage_Changing()
        {
            _blockManager.TryBuildGenesisBlock();
            Check_Random_Address_Storage_Changing();
            
            // using new chain id
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()));
            while(!HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()))
            {
                var block = BuildNextBlock();
                var result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Check_Random_Address_Storage_Changing();
        }

        [Test]
        public void Test_Wrong_Block_Emulation()
        {
            _blockManager.TryBuildGenesisBlock();
            var block = BuildNextBlock();
            block.Hash = UInt256Utils.Zero;

            var result = EmulateBlock(block);
            Assert.AreEqual(OperatingError.HashMismatched, result);

            block = BuildNextBlock();
            block.Header.Index = 0;

            result = EmulateBlock(block);
            Assert.AreEqual(OperatingError.HashMismatched, result);

            block = BuildNextBlock();
            block.Multisig = null;

            result = EmulateBlock(block);
            Assert.AreEqual(OperatingError.InvalidMultisig, result);
        }

        [Test]
        [Ignore("add long unit tests to separate test project")]
        public void Test_TxVerifierPerformanceCompare()
        {
            _blockManager.TryBuildGenesisBlock();
            _txVerifier.Start();
            int blockCount = 10;
            int txCount = 100;
            var startTime = TimeUtils.CurrentTimeMillis();
            AddSeveralBlocks(blockCount, txCount);
            var timePassed = TimeUtils.CurrentTimeMillis() - startTime;
            
            startTime = TimeUtils.CurrentTimeMillis();
            AddSeveralBlocks(blockCount, txCount, true);
            var timePassedWithTxVerifier = TimeUtils.CurrentTimeMillis() - startTime;
            
            startTime = TimeUtils.CurrentTimeMillis();
            AddSeveralBlocks(blockCount, txCount, true);
            timePassedWithTxVerifier += TimeUtils.CurrentTimeMillis() - startTime;

            startTime = TimeUtils.CurrentTimeMillis();
            AddSeveralBlocks(blockCount, txCount);
            timePassed += TimeUtils.CurrentTimeMillis() - startTime;
            Logger.LogInformation($"time passed with tx verifier: {timePassedWithTxVerifier} ms");
            Logger.LogInformation($"time passed without tx verifier: {timePassed} ms");
            Logger.LogInformation($"tx verifier is efficient? {timePassedWithTxVerifier < timePassed}");
            _txVerifier.Stop();
        }

        private void AddSeveralBlocks(int blockCount, int txCount, bool useTxVerifier = false)
        {
            var currentHeight = _blockManager.GetHeight();
            var persistedTx = 0;
            for (var iter = 0; iter < blockCount; iter++)
            {
                var topUpReceipts = new List<TransactionReceipt>();
                var randomReceipts = new List<TransactionReceipt>();
                var coverTxFeeAmount = Money.Parse("0.0000000001");
                for (var i = 0; i < txCount; i++)
                {
                    var tx = TestUtils.GetCustomTransaction("0", "0.000000000000000001", HardforkHeights.IsHardfork_9Active(currentHeight + 2));
                    randomReceipts.Add(tx);
                    topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                        (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i, 
                        HardforkHeights.IsHardfork_9Active(currentHeight + 1)));
                }

                bool topUpSucces = true;
                int txNo = 0;
                foreach (var tx in topUpReceipts)
                {
                    var added = _transactionPool.Add(tx);
                    if (added != OperatingError.UnsupportedTransaction && added != OperatingError.Ok)
                    {
                        Assert.That(false, $"top up tx not added to pool {added}, for block {currentHeight}, tx {txNo}");
                    }
                    if (added == OperatingError.UnsupportedTransaction)
                    {
                        topUpSucces = false;
                        break;
                    }
                    txNo++;
                }

                var takenTxes = _transactionPool.Peek(1000, 1000);
                if (useTxVerifier) _txVerifier.VerifyTransactions(takenTxes, HardforkHeights.IsHardfork_9Active(currentHeight + 1));
                var block = BuildNextBlock(takenTxes.ToArray());
                var result = ExecuteBlock(block, takenTxes.ToArray());
                Assert.AreEqual(result, OperatingError.Ok);
                var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
                if (topUpSucces) Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);
                else Assert.AreEqual(executedBlock!.TransactionHashes.Count, 0);
                currentHeight++;

                foreach (var tx in executedBlock.TransactionHashes)
                {
                    var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx);
                    if (!(receipt is null) && receipt.Status == TransactionStatus.Executed)
                        persistedTx++;
                }

                if (topUpSucces)
                {
                    foreach (var tx in randomReceipts)
                    {
                        var added = _transactionPool.Add(tx);
                        Assert.AreEqual(OperatingError.Ok, added);
                    }
                }

                takenTxes = _transactionPool.Peek(1000, 1000);
                if (useTxVerifier) _txVerifier.VerifyTransactions(takenTxes, HardforkHeights.IsHardfork_9Active(currentHeight + 1));
                block = BuildNextBlock(takenTxes.ToArray());
                result = ExecuteBlock(block, takenTxes.ToArray());
                Assert.AreEqual(result, OperatingError.Ok);
                executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
                if (topUpSucces) Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);
                else Assert.AreEqual(executedBlock!.TransactionHashes.Count, 0);
                currentHeight++;

                foreach (var tx in executedBlock.TransactionHashes)
                {
                    var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx);
                    if (!(receipt is null) && receipt.Status == TransactionStatus.Executed)
                        persistedTx++;
                }
            }

            Logger.LogTrace($"Tx persisted {persistedTx} out of {blockCount * 20}");
        }

        public void ExecuteTxesInSeveralBlocks(List<TransactionReceipt> txes)
        {
            txes = txes.OrderBy(x => x, new ReceiptComparer()).ToList();
            foreach (var tx in txes)
            {
                _transactionPool.Add(tx);
            }
            int total = 10;
            for (int it = 0; it < total; it++)
            {
                var remBlocks = total - it;
                var txesToTake = txes.Count / remBlocks;
                var takenTxes = _transactionPool.Peek(txesToTake, txesToTake);
                var block = BuildNextBlock(takenTxes.ToArray());
                var result = ExecuteBlock(block, takenTxes.ToArray());
                Assert.AreEqual(result, OperatingError.Ok);
                var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(block.Header.Index);
                Assert.AreEqual(executedBlock!.TransactionHashes.Count, takenTxes.Count);

                // check if the txes are executed properly
                foreach (var tx in takenTxes)
                {
                    var executedTx = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(tx.Hash);
                    Assert.AreNotEqual(null, executedTx, $"Transaction {tx.Hash.ToHex()} not found");
                    Assert.AreEqual(TransactionStatus.Executed, executedTx!.Status,
                        "Transaction {tx.Hash.ToHex()} was not executed properly");
                }
            }
        }

        private void Check_Random_Address_Storage_Changing()
        {
            var randomTx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
            var balance = randomTx.Transaction.Value;
            var allowance = balance;
            var receiver = randomTx.Transaction.From;

            var tx1 = TopUpBalanceTx(receiver, balance, 0, 
                HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
            var tx2 = ApproveTx(receiver, allowance, 0, 
                HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));

            var owner = tx2.Transaction.From;

            var blockTxs = new[] {tx1, tx2};

            var topUpBlock = BuildNextBlock(blockTxs);
            ExecuteBlock(topUpBlock, blockTxs);

            var storedBalance = _stateManager.LastApprovedSnapshot.Balances.GetBalance(receiver);
            Assert.AreEqual(balance.ToMoney(), storedBalance);

            var storedAllowance = _stateManager.LastApprovedSnapshot.Storage.GetRawValue(
                ContractRegisterer.LatokenContract,
                UInt256Utils.Zero.Buffer.Concat(owner.ToBytes().Concat(receiver.ToBytes()))
            ).ToUInt256().ToMoney();
            Assert.AreEqual(allowance.ToMoney(), storedAllowance);
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

        private OperatingError EmulateBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };
            var (status, _, _, _) = _blockManager.Emulate(block, receipts);
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

        private TransactionReceipt ApproveTx(UInt160 to, UInt256 value, int nonceInc,  bool useNewChainId)
        {
            var input = ContractEncoder.Encode(Lrc20Interface.MethodApprove, to, value);
            var tx = new Transaction
            {
                To = ContractRegisterer.LatokenContract,
                Invocation = ByteString.CopyFrom(input),
                From = _wallet.EcdsaKeyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 10_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(_wallet.EcdsaKeyPair.PublicKey.GetAddress()) +
                        (ulong) nonceInc,
                Value = UInt256Utils.Zero,
            };
            return Signer.Sign(tx, _wallet.EcdsaKeyPair, useNewChainId);
        }
    }
}