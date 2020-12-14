using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
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
using Lachain.Crypto.Misc;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    [TestFixture]
    public class BlocksTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IContainer? _container;

        [SetUp]
        public void Setup()
        {
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
            TestUtils.DeleteTestChainData();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_Genesis()
        {
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
            Assert.AreEqual(
                "0x58272e9099317e9fb283b8f0f951dd156b1983d7e008c1cf811658d19d7aa36b".HexToUInt256(),
                genesis.Header.StateHash
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
        }

        [Test]
        [Repeat(5)]
        public void Test_Block_With_Txs_Execution()
        {
            _blockManager.TryBuildGenesisBlock();
            var topUpReceipts = new List<TransactionReceipt>();
            var randomReceipts = new List<TransactionReceipt>();
            var txCount = new Random().Next(1, 50);

            var coverTxFeeAmount = Money.Parse("10.0");
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransaction();
                randomReceipts.Add(tx);
                topUpReceipts.Add(TopUpBalanceTx(tx.Transaction.From,
                    (tx.Transaction.Value.ToMoney() + coverTxFeeAmount).ToUInt256(), i));
            }

            var topUpBlock = BuildNextBlock(topUpReceipts.ToArray());
            var topUpResult = ExecuteBlock(topUpBlock, topUpReceipts.ToArray());
            Assert.AreEqual(topUpResult, OperatingError.Ok);

            var randomBlock = BuildNextBlock(randomReceipts.ToArray());
            var result = ExecuteBlock(randomBlock, randomReceipts.ToArray());
            Assert.AreEqual(result, OperatingError.Ok);

            var executedBlock = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(randomBlock.Header.Index);
            Assert.AreEqual(executedBlock!.TransactionHashes.Count, txCount);
        }

        [Test]
        public void Test_Storage_Changing()
        {
            _blockManager.TryBuildGenesisBlock();

            var randomTx = TestUtils.GetRandomTransaction();
            var balance = randomTx.Transaction.Value;
            var allowance = balance;
            var receiver = randomTx.Transaction.From;

            var tx1 = TopUpBalanceTx(receiver, balance, 0);
            var tx2 = ApproveTx(receiver, allowance, 1);

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

        private Block BuildNextBlock(TransactionReceipt[] receipts = null)
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

        private OperatingError ExecuteBlock(Block block, TransactionReceipt[] receipts = null)
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

        private OperatingError EmulateBlock(Block block, TransactionReceipt[] receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };
            var (status, _, _, _) = _blockManager.Emulate(block, receipts);
            return status;
        }

        private TransactionReceipt TopUpBalanceTx(UInt160 to, UInt256 value, int nonceInc)
        {
            var tx = new Transaction
            {
                To = to,
                From = _wallet.EcdsaKeyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 4_000_000,
                Nonce = _transactionPool.GetNextNonceForAddress(_wallet.EcdsaKeyPair.PublicKey.GetAddress()) +
                        (ulong) nonceInc,
                Value = value
            };
            return Signer.Sign(tx, _wallet.EcdsaKeyPair);
        }

        private TransactionReceipt ApproveTx(UInt160 to, UInt256 value, int nonceInc)
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
            return Signer.Sign(tx, _wallet.EcdsaKeyPair);
        }
    }
}