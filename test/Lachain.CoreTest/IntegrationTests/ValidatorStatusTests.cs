using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class ValidatorStatusTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IValidatorStatusManager _validatorStatusManager = null!;
        private IConfigManager _configManager = null!;
        private IContainer? _container;

        public ValidatorStatusTest()
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
            _container?.Dispose();
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
            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
            }
            _validatorStatusManager = new ValidatorStatusManager(
                _transactionPool, _container.Resolve<ITransactionSigner>(), _container.Resolve<ITransactionBuilder>(),
                _wallet, _stateManager, _container.Resolve<IValidatorAttendanceRepository>(),
                _container.Resolve<ISystemContractReader>()
            );
        }

        [TearDown]
        public void Teardown()
        {
            _validatorStatusManager.Stop();
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        // TODO: this is not working since we have only 1 validator
        [Test]
        public void Test_StakeWithdraw()
        {
            _blockManager.TryBuildGenesisBlock();
            GenerateBlocks(1, 1);

            _validatorStatusManager.Start(false);
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsFalse(_validatorStatusManager.IsWithdrawTriggered());

            int blockNum = (int) _blockManager.GetHeight();
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active((ulong) blockNum));
            while(!HardforkHeights.IsHardfork_9Active((ulong) blockNum))
            {
                blockNum++;
                GenerateBlocks(blockNum, blockNum);
            }

            var systemContractReader = _container?.Resolve<ISystemContractReader>() ?? throw new Exception("Container is not loaded");
            var stake = new Money(systemContractReader.GetStake());
            Console.WriteLine($"Current stake is {stake}");
            Assert.That(stake > Money.Zero, "Stake is zero");

            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            // Test node is the only validator, so it is a next validator always 
            // and it can't withdraw its stake. TODO: test to check withdraw is working
            //GenerateBlocks(50);
            //Assert.IsFalse(_validatorStatusManager.IsStarted());
        }

        [Test]
        public void Test_StakeSize()
        {
            _blockManager.TryBuildGenesisBlock();
            var systemContractReader = _container?.Resolve<ISystemContractReader>() ?? throw new Exception("Container is not loaded");
            var tx = TopUpBalanceTx(systemContractReader.NodeAddress(),Money.Parse("3000.0").ToUInt256(), 0, 
                HardforkHeights.IsHardfork_9Active(1));
            Assert.AreEqual(OperatingError.Ok, _transactionPool.Add(tx));
            GenerateBlocks(1, 1);
            var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(systemContractReader.NodeAddress());
            var placeToStake = Money.Parse("2000.0");
            Assert.That(balance > placeToStake, "Not enough balance to make stake");
            _validatorStatusManager.StartWithStake(placeToStake.ToUInt256());
            Assert.That(_validatorStatusManager.IsStarted(), "Manager is not started");
            Assert.That(!_validatorStatusManager.IsWithdrawTriggered(), "Withdraw was triggered from the beggining");
            
            int blockNum = (int) _blockManager.GetHeight();
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active((ulong) blockNum));
            while(!HardforkHeights.IsHardfork_9Active((ulong) blockNum))
            {
                blockNum++;
                GenerateBlocks(blockNum, blockNum);
            }
            
            var stake = new Money(systemContractReader.GetStake(systemContractReader.NodeAddress()));
            // TODO: fix it
//            Assert.That(stake == placeToStake, $"Stake is not as intended: {stake} != {placeToStake}");
            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.That(_validatorStatusManager.IsStarted(), "Manager is stopped right after withdraw request");
            Assert.That(_validatorStatusManager.IsWithdrawTriggered(), "Withdraw is not triggered");
            // Test node is the only vaidator, so it is a next validator always 
            // and it can't withdraw its stake. 
            //    GenerateBlocks(50);
            //    Assert.That(!_validatorStatusManager.IsStarted(), "Manager is not stopped");
            //_validatorStatusManager.Stop();
        }
        

        private void GenerateBlocks(int from, int to)
        {
            for (int i = from; i <= to; i++)
            {
                var txes = GetCurrentPoolTxes((ulong) i);
                var block = BuildNextBlock(txes);
                var result = ExecuteBlock(block, txes);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes(ulong era)
        {
            return _transactionPool.Peek(1000, 1000).ToArray();
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
                TransactionHashes = { receipts.Select(tx => tx.Hash) },
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
                Validators = { _wallet.EcdsaKeyPair.PublicKey },
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
    }
}