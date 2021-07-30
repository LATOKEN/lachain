using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Google.Protobuf;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts;
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
using Lachain.UtilityTest;
using MCL.BLS12_381.Net;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class GovernanceEventsTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();
        private static readonly ulong CycleDuration = StakingContract.CycleDuration;

        private IBlockManager _blockManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IContainer? _container;
        private Dictionary<UInt256, ByteString> _eventData = null!;

        public GovernanceEventsTest()
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
            _eventData = new Dictionary<UInt256, ByteString>();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _blockManager = _container.Resolve<IBlockManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_EventFormat()
        {
            _blockManager.TryBuildGenesisBlock();

            for (var i = 0; i < (int)(CycleDuration + 2); i++)
            {
                GenerateBlocks(1);
                var lastblock =
                    _stateManager.CurrentSnapshot.Blocks.GetBlockByHeight(_stateManager.CurrentSnapshot.Blocks
                        .GetTotalBlockHeight()) ?? throw new Exception("No last block?");
                foreach (UInt256 tx in lastblock.TransactionHashes)
                {
                    var eventCount = _stateManager.CurrentSnapshot.Events.GetTotalTransactionEvents(tx);
                    Console.WriteLine($"{eventCount} events");
                    for (uint j = 0; j < eventCount; j++)
                    {
                        var eventObj = _stateManager.CurrentSnapshot.Events.GetEventByTransactionHashAndIndex(tx, j)
                                       ?? throw new Exception($"No event {j} in {tx.ToHex()}");
                        Assert.AreEqual(eventObj.TransactionHash, tx);
                        if (_eventData.TryGetValue(eventObj.TransactionHash, out var storedData))
                            Assert.AreEqual(eventObj.Data.ToHex(), storedData.ToHex());
                    }
                }
            }
        }

        private void GenerateBlocks(int blockNum)
        {
            for (var i = 0; i < blockNum; i++)
            {
                var txs = GetCurrentPoolTxs();
                var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
                if (height % CycleDuration == CycleDuration - 1) // next block is last in cycle
                {
                    var newTxs = new TransactionReceipt[txs.Length + 1];
                    txs.CopyTo(newTxs, 0);
                    newTxs[txs.Length] = MakeDistributeCycleRewardsAndPenaltiesTxReceipt();
                    txs = newTxs;
                    Console.WriteLine($"Tx with a contract call, {txs.Length} txs");
                }

                if (height % CycleDuration == 0)
                {
                    var newTxs = new TransactionReceipt[txs.Length + 1];
                    txs.CopyTo(newTxs, 0);
                    newTxs[txs.Length] = MakeCommitTransaction();
                    txs = newTxs;
                    Console.WriteLine($"Tx with a contract call, {txs.Length} txs");
                }

                if (height % CycleDuration == 1)
                {
                    var newTxs = new TransactionReceipt[txs.Length + 1];
                    txs.CopyTo(newTxs, 0);
                    newTxs[txs.Length] = MakeNextValidatorsTxReceipt();
                    txs = newTxs;
                    Console.WriteLine($"Tx with a contract call, {txs.Length} txs");
                }


                var block = BuildNextBlock(txs);
                var result = ExecuteBlock(block, txs);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxs()
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

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);

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

        private OperatingError ExecuteBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };

            var (_, _, stateHash, _) = _blockManager.Emulate(block, receipts);

            var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
            var predecessor =
                _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(height);
            var (header, multisig) = BuildHeaderAndMultisig(block.Header.MerkleRoot, predecessor, stateHash);

            block.Header = header;
            block.Multisig = multisig;
            block.Hash = header.Keccak();

            var status = _blockManager.Execute(block, receipts, true, true);
            return status;
        }

        private OperatingError EmulateBlock(Block block, TransactionReceipt[]? receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };
            var (status, _, _, _) = _blockManager.Emulate(block, receipts);
            return status;
        }

        private TransactionReceipt MakeDistributeCycleRewardsAndPenaltiesTxReceipt()
        {
            var res = BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract,
                GovernanceInterface.MethodDistributeCycleRewardsAndPenalties); 
            Assert.False(_eventData.ContainsKey(res.Hash));
            // TODO: remove hardcoded block reward with value from settings
            var totalReward = Money.Parse("5.0") * (int) StakingContract.CycleDuration;
            _eventData.Add(res.Hash,
                ByteString.CopyFrom(ContractEncoder.Encode(
                    GovernanceInterface.EventDistributeCycleRewardsAndPenalties,
                    totalReward)));
            return res;
        }

        private TransactionReceipt MakeNextValidatorsTxReceipt()
        {
            var sk = Crypto.GeneratePrivateKey();
            var pk = Crypto.ComputePublicKey(sk, false);
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _wallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodChangeValidators,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight())),
                (pk)
            );
            var res = Signer.Sign(tx, _wallet.EcdsaKeyPair);
            Assert.False(_eventData.ContainsKey(res.Hash));
            _eventData.Add(res.Hash,
                ByteString.CopyFrom(ContractEncoder.Encode(GovernanceInterface.EventChangeValidators, (pk))));
            return res;
        }

        private TransactionReceipt MakeKeygenSendValuesTxReceipt()
        {
            var proposer = new BigInteger(0).ToUInt256();
            var value = new Byte[0];
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _wallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenSendValue,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight())),
                proposer, (value)
            );
            var res = Signer.Sign(tx, _wallet.EcdsaKeyPair);
            Assert.False(_eventData.ContainsKey(res.Hash));
            _eventData.Add(res.Hash,
                ByteString.CopyFrom(ContractEncoder.Encode(GovernanceInterface.EventKeygenSendValue,
                    proposer, (value))));
            return res;
        }

        private TransactionReceipt MakeFinishCircleTxReceipt()
        {
            var res = BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract,
                GovernanceInterface.MethodFinishCycle);
            Assert.False(_eventData.ContainsKey(res.Hash));
            _eventData.Add(res.Hash,
                ByteString.CopyFrom(ContractEncoder.Encode(GovernanceInterface.EventFinishCycle)));
            return res;
        }

        private TransactionReceipt MakeCommitTransaction()
        {
            var biVarPoly = BiVarSymmetricPolynomial.Random(0);
            var commitment = biVarPoly.Commit().ToBytes();
            var row = new Byte[0];
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _wallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodKeygenCommit,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight())),
                commitment,
                new byte[][] {row}
            );

            var res = Signer.Sign(tx, _wallet.EcdsaKeyPair);
            Assert.False(_eventData.ContainsKey(res.Hash));
            _eventData.Add(res.Hash,
                ByteString.CopyFrom(ContractEncoder.Encode(GovernanceInterface.EventKeygenCommit,
                    commitment, new byte[][] {row})));
            return res;
        }

        private TransactionReceipt BuildSystemContractTxReceipt(UInt160 contractAddress, string mehodSignature)
        {
            var transaction = _transactionBuilder.InvokeTransactionWithGasPrice(
                UInt160Utils.Zero,
                contractAddress,
                Money.Zero,
                mehodSignature,
                0,
                UInt256Utils.ToUInt256(GovernanceContract.GetCycleByBlockNumber(_stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight()))
            );
            return new TransactionReceipt
            {
                Hash = transaction.FullHash(SignatureUtils.Zero),
                Status = TransactionStatus.Pool,
                Transaction = transaction,
                Signature = SignatureUtils.Zero,
            };
        }
    }
}