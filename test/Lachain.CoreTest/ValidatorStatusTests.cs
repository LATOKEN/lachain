using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.DI;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
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
        private INetworkManager _networkManager;
        private IContainer? _container;
        
        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
            Directory.CreateDirectory("./ChainLachain"); // TODO: this is some dirty hack, hub creates file not in correct place
            var containerBuilder = TestUtils.GetContainerBuilder(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json")
            );
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _networkManager = _container.Resolve<INetworkManager>();
            _networkManager.Start();
            _validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_StakeWithdraw()
        {
            _blockManager.TryBuildGenesisBlock();
            GenerateBlocks(1);

            _validatorStatusManager.Start(false);
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsFalse(_validatorStatusManager.IsWithdrawTriggered());

            GenerateBlocks(50);

            var systemContractReader = _container.Resolve<ISystemContractReader>();
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
            GenerateBlocks(1);

            var systemContractReader = _container.Resolve<ISystemContractReader>();
            var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(systemContractReader.NodeAddress());
            var placeToStake = Money.Parse("2000.0");
            Assert.That(balance > placeToStake, "Not enough balance to make stake");
            _validatorStatusManager.StartWithStake(placeToStake.ToUInt256(false));
            Assert.That(_validatorStatusManager.IsStarted(), "Manager is not started");
            Assert.That(!_validatorStatusManager.IsWithdrawTriggered(), "Withdraw was triggered from the beggining");

            GenerateBlocks(50);

            var stake = new Money(systemContractReader.GetStake(), true);
            Assert.That(stake == placeToStake,$"Stake is not as intended: {stake} != {placeToStake}");

            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.That(_validatorStatusManager.IsStarted(), "Manager is stopped right after withdraw request");
            Assert.That(_validatorStatusManager.IsWithdrawTriggered(), "Withdraw is not triggered");

            // Test node is the only vaidator, so it is a next validator always 
            // and it can't withdraw its stake. 
            //    GenerateBlocks(50);
            //    Assert.That(!_validatorStatusManager.IsStarted(), "Manager is not stopped");
        }

        private void GenerateBlocks(int blockNum)
        {
            for (int i = 0; i < blockNum; i++)
            {
                var txes = GetCurrentPoolTxes();
                var block = BuildNextBlock(txes);
                var result = ExecuteBlock(block, txes);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes()
        {
            return _transactionPool.Peek(1000, 1000).ToArray();
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
    }
}