using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class MessageSenderTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();
        
        private IBlockManager _blockManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
        private IConfigManager _configManager = null!;
        private IContainer? _container;

        [SetUp]
        public void Setup()
        {
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
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _stateManager = _container.Resolve<IStateManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _configManager = _container.Resolve<IConfigManager>();
            // set chainId from config
            if (TransactionUtils.ChainId == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!);
            }
            _blockManager.TryBuildGenesisBlock();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }
        
        [Test]
        public void Test_MessageSender()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceTest = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.test.wasm");
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlock(1);
            
            // Deploy caller contract 
            if(resourceTest is null)
                Assert.Fail("Failed to read bytecode from resources");
            var byteCode = new byte[resourceTest!.Length];
            resourceTest!.Read(byteCode, 0, (int)resourceTest!.Length);
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate contract code");
            var from = keyPair.PublicKey.GetAddress();
            var fromReverted = from.ToBytes().Reverse().ToArray().ToUInt160();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlock(2);
            
            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed contract");
            
            // invoke caller contract 
            var transactionReceipt = new TransactionReceipt();
            transactionReceipt.Transaction = new Transaction();
            transactionReceipt.Transaction.Value = 0.ToUInt256();
            var abi = ContractEncoder.Encode("testMsgSender()");
            var invocationResult = VirtualMachine.InvokeWasmContract(
                contract!,
                new InvocationContext(from, _stateManager.LastApprovedSnapshot, transactionReceipt),
                abi,
                GasMetering.DefaultBlockGasLimit
            );
            Assert.That(invocationResult.Status == ExecutionStatus.Ok, "Failed to invoke contract");
            Assert.That(invocationResult.GasUsed > 0, "No gas used during contract invocation");
            Assert.AreEqual(from.ToUInt256().ToHex(), invocationResult.ReturnValue!.ToHex(), 
                "Invalid invocation return value");
        }

        private void GenerateBlock(int blockNum)
        {
            var txs = GetCurrentPoolTxs((ulong) blockNum);
            var block = BuildNextBlock(txs);
            var result = ExecuteBlock(block, txs);
            Assert.AreEqual(OperatingError.Ok, result);
        }

        private TransactionReceipt[] GetCurrentPoolTxs(ulong era)
        {
            return _transactionPool.Peek(1000, 1000, era).ToArray();
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
    }
}