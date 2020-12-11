using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Lachain.Consensus;
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
    public class GovernanceEventsTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();

        private IBlockManager _blockManager = null!;
        private IBlockProducer _blockProducer = null!;
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
            _blockProducer = _container.Resolve<IBlockProducer>();
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
        public void Test_CountEvents()
        {
            _blockManager.TryBuildGenesisBlock();
            _validatorStatusManager.Start(false);

            for (int i = 0; i < 60; i++)
            {
                GenerateBlocks(1);

                var lastblock = _stateManager.CurrentSnapshot.Blocks.GetBlockByHeight(_stateManager.CurrentSnapshot.Blocks.GetTotalBlockHeight());
                foreach (UInt256 tx in lastblock?.TransactionHashes)
                {
                    var event_count = _stateManager.CurrentSnapshot.Events.GetTotalTransactionEvents(tx);
                    Console.WriteLine($"{event_count} events");
                    for(uint j = 0; j < event_count; j++)
                    {
                        var event_obj = _stateManager.CurrentSnapshot.Events.GetEventByTransactionHashAndIndex(tx, j);
                        Console.WriteLine($"Event {j}: [{event_obj}]");
                        Assert.AreEqual(event_obj.TransactionHash, tx);
                    }
                }
            }

            Assert.IsTrue(false);
        }


        private void GenerateBlocks(int blockNum)
        {
            for (int i = 0; i < blockNum; i++)
            {
                var txes = GetCurrentPoolTxes();
                var height = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight();
                if (height % 50 == 0)
                {
                    var new_txes = new TransactionReceipt[txes.Length + 1];
                    txes.CopyTo(new_txes, 0);
                    new_txes[txes.Length] = BuildSystemContractTxReceipt(ContractRegisterer.GovernanceContract,
                        GovernanceInterface.MethodFinishCycle);
                    txes = new_txes;
                    Console.WriteLine($"Tx with a contract call, {txes.Length} txes");
                }

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
                TransactionHashes = { receipts.Select(tx => tx.Hash)},
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

        private OperatingError EmulateBlock(Block block, TransactionReceipt[] receipts = null)
        {
            receipts ??= new TransactionReceipt[] { };
            var (status, _, _, _) = _blockManager.Emulate(block, receipts);
            return status;
        }

        private TransactionReceipt BuildSystemContractTxReceipt(UInt160 contractAddress, string mehodSignature)
        {
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(UInt160Utils.Zero);
            var abi = ContractEncoder.Encode(mehodSignature);
            var transaction = new Transaction
            {
                To = contractAddress,
                Value = UInt256Utils.Zero,
                From = UInt160Utils.Zero,
                Nonce = nonce,
                GasPrice = 0,
                /* TODO: gas estimation */
                GasLimit = 100000000,
                Invocation = ByteString.CopyFrom(abi),
            };
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