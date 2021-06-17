using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
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
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class ContractTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new TransactionSigner();
        
        /*
         * Contract bytecode. It's C++ code is:
            #include <lachain/lachain.h>
            using namespace lachain;
            LACHAIN_ABI("test()", "uint8")
            static uint8 test() {
                return uint8(42);
            }
            static void fallback() {
                system_halt(HALT_CODE_UNKNOWN_METHOD);
            }
         */
        private static readonly string ByteCodeHex = "0061736d010000000117056000017f60037f7f7f0060017f0060027f7f00600" +
                                                     "000024e0403656e760d6765745f63616c6c5f73697a65000003656e760f636f" +
                                                     "70795f63616c6c5f76616c7565000103656e760b73797374656d5f68616c740" +
                                                     "00203656e760a7365745f72657475726e000303020104040501700101010503" +
                                                     "0100020608017f01418088040b071202066d656d6f727902000573746172740" +
                                                     "0040a880101850101027f23808080800041106b220024808080800002400240" +
                                                     "024002401080808080004104490d00410041042000410b6a108180808000200" +
                                                     "028000b220141f8d1f6ef06460d012001417f470d020b41051082808080000c" +
                                                     "020b2000412a3a000f2000410f6a41011083808080000c010b4105108280808" +
                                                     "0000b200041106a2480808080000b0048046e616d65014105000d6765745f63" +
                                                     "616c6c5f73697a65010f636f70795f63616c6c5f76616c7565020b737973746" +
                                                     "56d5f68616c74030a7365745f72657475726e0405737461727400760970726f" +
                                                     "647563657273010c70726f6365737365642d62790105636c616e675631312e3" +
                                                     "02e30202868747470733a2f2f6769746875622e636f6d2f6c6c766d2f6c6c76" +
                                                     "6d2d70726f6a656374203137363234396264363733326138303434643435373" +
                                                     "039326564393332373638373234613666303629";
        
        private IBlockManager _blockManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IPrivateWallet _wallet = null!;
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
            _blockManager.TryBuildGenesisBlock();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_ContractDeployAndInvoke()
        {
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlocks(1);
            
            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(1);
            
            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed contract");
            
            // invoke deployed contract without blockchain
            var abi = ContractEncoder.Encode("test()");
            var invocationResult = VirtualMachine.InvokeWasmContract(
                contract!,
                new InvocationContext(from, _stateManager.LastApprovedSnapshot, new TransactionReceipt
                {
                    // TODO: correctly fill these fields
                    Block = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight(),
                }),
                abi,
                GasMetering.DefaultBlockGasLimit
            );
            Assert.That(invocationResult.Status == ExecutionStatus.Ok, "Failed to invoke deployed contract");
            Assert.That(invocationResult.GasUsed > 0, "No gas used during contract invocation");
            Assert.That(invocationResult.ReturnValue!.ToHex() == "0x2a", "Invalid invocation return value");
            
            // invoke contract in blockchain
            var txInvoke = _transactionBuilder.InvokeTransaction(
                keyPair.PublicKey.GetAddress(),
                contractHash,
                Money.Zero,
                "test()",
                new dynamic[0]);
            var signedTxInvoke = Signer.Sign(txInvoke, keyPair);
            var error = _transactionPool.Add(signedTxInvoke);
            Assert.That(error == OperatingError.Ok, "Failed to add invoke tx to pool");
            GenerateBlocks(1);
            var tx2 = 
                _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(signedTxInvoke.Hash);
            Assert.That(tx2 != null, "Failed to add invoke tx to block");
            Assert.That(tx2!.Status == TransactionStatus.Executed, "Failed to execute invoke tx");
            Assert.That(tx2.GasUsed > 0, "Invoke tx didn't use gas");
            // Invocation result is not stored in TransactionReceipt now, can't verify it
        }

        [Test]
        public void Test_ContractFromContractInvoke()
        {
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlocks(1);
            
            // Deploy callee contract 
            var byteCode = File.ReadAllBytes("Deployed.wasm");
            Assert.That(VirtualMachine.VerifyContract(byteCode), "Unable to validate callee code");
            var from = keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(1);
            
            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed callee contract");
            
            // Deploy caller contract 
            byteCode = File.ReadAllBytes("Existing.wasm");
            Assert.That(VirtualMachine.VerifyContract(byteCode), "Unable to validate caller code");
            nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            tx = _transactionBuilder.DeployTransaction(from, byteCode);
            signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(1);
            
            // check contract is deployed
            contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed caller contract");
            
            // invoke caller contract 
            var abi = ContractEncoder.Encode("get()");
            var invocationResult = VirtualMachine.InvokeWasmContract(
                contract!,
                new InvocationContext(from, _stateManager.LastApprovedSnapshot, new TransactionReceipt
                {
                    // TODO: correctly fill these fields
                    Block = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight(),
                }),
                abi,
                GasMetering.DefaultBlockGasLimit
            );
            Assert.That(invocationResult.Status == ExecutionStatus.Ok, "Failed to invoke deployed contract");
            Assert.That(invocationResult.GasUsed > 0, "No gas used during contract invocation");
            Assert.That(invocationResult.ReturnValue!.ToHex() == "0x2a", "Invalid invocation return value");
        }

        private void GenerateBlocks(int blockNum)
        {
            for (var i = 0; i < blockNum; i++)
            {
                var txs = GetCurrentPoolTxs();
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
    }
}