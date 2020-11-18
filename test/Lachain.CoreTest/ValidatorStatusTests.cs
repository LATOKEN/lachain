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
using Lachain.Core.ValidatorStatus;
using Lachain.Core.DI;
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
    public class ValidatorStatusTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();
        
        private IBlockManager _blockManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IValidatorStatusManager _validatorStatusManager = null!;
        private IContainer? _container;
        
        [SetUp]
        public void Setup()
        {
            var containerBuilder = TestUtils.GetContainerBuilder(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json")
            );
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            TestUtils.DeleteTestChainData();
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
            var block = BuildNextBlock();
            var result = ExecuteBlock(block);
            Assert.AreEqual(OperatingError.Ok, result);

            _validatorStatusManager.Start(false);
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsFalse(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 50; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }

            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 50; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsFalse(_validatorStatusManager.IsStarted());
        }

        public void Test_StakeWithdrawWrongStage()
        {
            _blockManager.TryBuildGenesisBlock();
            var block = BuildNextBlock();
            var result = ExecuteBlock(block);
            Assert.AreEqual(OperatingError.Ok, result);

            _validatorStatusManager.Start(false);
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsFalse(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 50; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }

            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 10; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            _validatorStatusManager.Start(false);
            for (int i = 0; i < 10; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            _validatorStatusManager.WithdrawStakeAndStop();
            for (int i = 0; i < 10; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 20; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsFalse(_validatorStatusManager.IsStarted());
        }

        [Test]
        public void Test_StakeSize()
        {
            _blockManager.TryBuildGenesisBlock();
            var block = BuildNextBlock();
            var result = ExecuteBlock(block);
            Assert.AreEqual(OperatingError.Ok, result);

            var systemContractReader = _container.Resolve<ISystemContractReader>();
            var balance = _stateManager.CurrentSnapshot.Balances.GetBalance(systemContractReader.NodeAddress());
            var placeToStake = Money.Parse("2000.0");
            Assert.That(balance > placeToStake, "Not enosh balance tpo make stake");
            _validatorStatusManager.StartWithStake(placeToStake.ToUInt256());
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsFalse(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 50; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }

            var stake = systemContractReader.GetStake();
            Assert.AreEqual(stake, placeToStake);

            _validatorStatusManager.WithdrawStakeAndStop();
            Assert.IsTrue(_validatorStatusManager.IsStarted());
            Assert.IsTrue(_validatorStatusManager.IsWithdrawTriggered());

            for (int i = 0; i < 50; i++)
            {
                block = BuildNextBlock();
                result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            Assert.IsFalse(_validatorStatusManager.IsStarted());
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