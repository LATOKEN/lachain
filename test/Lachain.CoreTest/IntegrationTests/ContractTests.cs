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
        private static readonly string ByteCodeHex = "0061736D01000000011C0660017F006000017F60037F7F7F0060027F7F0060017F017F60000002610503656E760C6765745F6D736776616C7565000003656E760D6765745F636F64655F73697A65000103656E760F636F70795F636F64655F76616C7565000203656E760A7365745F72657475726E000303656E760B73797374656D5F68616C74000003040304050505030100020608017F01418080040B071202066D656D6F7279020005737461727400070ACE0203A60101047F418080042101024003400240200128020C0D002001280208220220004F0D020B200128020022010D000B41002101410028020821020B02402002200041076A41787122036B22024118490D00200120036A41106A22002001280200220436020002402004450D00200420003602040B2000200241706A3602082000410036020C2000200136020420012000360200200120033602080B2001410136020C200141106A0B2E004100410036028080044100410036028480044100410036028C800441003F0041107441F0FF7B6A36028880040B7501027F230041206B220024002000100002402000290300200041106A29030084200041086A290300200041186A29030084844200520D0010064100100141D5726A220036029007410020001005220136029407200141AB0D200010024100418F07100341001004000B41004100100341011004000B0B9607010041000B8F070061736D01000000011C0660017F006000017F60037F7F7F0060027F7F0060017F017F60000002610503656E760C6765745F6D736776616C7565000003656E760D6765745F63616C6C5F73697A65000103656E760F636F70795F63616C6C5F76616C7565000203656E760A7365745F72657475726E000303656E760B73797374656D5F68616C7400000305040304050505030100020608017F01418080040B071202066D656D6F7279020005737461727400080ACC0304240002402001450D00034020004200370300200041086A21002001417F6A22010D000B0B0BA60101047F418080042101024003400240200128020C0D002001280208220220004F0D020B200128020022010D000B41002101410028020821020B02402002200041076A41787122036B22024118490D00200120036A41106A22002001280200220436020002402004450D00200420003602040B2000200241706A3602082000410036020C2000200136020420012000360200200120033602080B2001410136020C200141106A0B2E004100410036028080044100410036028480044100410036028C800441003F0041107441F0FF7B6A36028880040BCD0101037F230041306B22002400200041086A10000240024002402000290308200041186A29030084200041106A290300200041206A29030084844200520D00100741001001220136020441002001100622023602084100200120021002200141034D0D01410020022802002201360200200141F8D1F6EF06470D012000412A3A002F4100450D0241004100100341011004000B41004100100341011004000B41004100100341011004000B412010062201410410052001411F6A20002D002F3A000020014120100341001004000B007D0970726F647563657273010C70726F6365737365642D62790105636C616E675D31322E302E31202868747470733A2F2F6769746875622E636F6D2F736F6C616E612D6C6162732F6C6C766D2D70726F6A656374203734373435346230616535353664613132306239616431363435613133653438633430363031613629008B01046E616D65017009000C6765745F6D736776616C7565010D6765745F63616C6C5F73697A65020F636F70795F63616C6C5F76616C7565030A7365745F72657475726E040B73797374656D5F68616C7405085F5F627A65726F3806085F5F6D616C6C6F63070B5F5F696E69745F6865617008057374617274071201000F5F5F737461636B5F706F696E746572007D0970726F647563657273010C70726F6365737365642D62790105636C616E675D31322E302E31202868747470733A2F2F6769746875622E636F6D2F736F6C616E612D6C6162732F6C6C766D2D70726F6A656374203734373435346230616535353664613132306239616431363435613133653438633430363031613629008D01046E616D65016608000C6765745F6D736776616C7565010D6765745F636F64655F73697A65020F636F70795F636F64655F76616C7565030A7365745F72657475726E040B73797374656D5F68616C7405085F5F6D616C6C6F63060B5F5F696E69745F6865617007057374617274071201000F5F5F737461636B5F706F696E746572090A0100072E726F64617461";
        
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
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
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
        public void Test_ContractDeployAndInvoke()
        {
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlock(1);
            
            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair, true);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlock(2);
            
            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed contract");
            
            // invoke deployed contract without blockchain
            var abi = ContractEncoder.Encode("test()");
            var transactionReceipt = new TransactionReceipt {
                Block = _stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight(),
            };
            transactionReceipt.Transaction = new Transaction();
            transactionReceipt.Transaction.Value = 0.ToUInt256();
            var invocationResult = VirtualMachine.InvokeWasmContract(
                contract!,
                new InvocationContext(from, _stateManager.LastApprovedSnapshot, transactionReceipt),
                abi,
                GasMetering.DefaultBlockGasLimit
            );
            Assert.That(invocationResult.Status == ExecutionStatus.Ok, "Failed to invoke deployed contract");
            Assert.That(invocationResult.GasUsed > 0, "No gas used during contract invocation");
            Assert.That(invocationResult.ReturnValue!.ToHex() == "0x000000000000000000000000000000000000000000000000000000000000002a", "Invalid invocation return value");
            
            // invoke contract in blockchain
            var txInvoke = _transactionBuilder.InvokeTransaction(
                keyPair.PublicKey.GetAddress(),
                contractHash,
                Money.Zero,
                "test()",
                new dynamic[0]);
            var signedTxInvoke = Signer.Sign(txInvoke, keyPair, true);
            var error = _transactionPool.Add(signedTxInvoke);
            Assert.That(error == OperatingError.Ok, "Failed to add invoke tx to pool");
            GenerateBlock(3);
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
            var assembly = Assembly.GetExecutingAssembly();
            var resourceC = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.C.wasm");
            var resourceD = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.D.wasm");
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlock(1);
            
            // Deploy callee contract 
            if(resourceD is null)
                Assert.Fail("Failed t read script from resources");
            var byteCode = new byte[resourceD!.Length];
            resourceD!.Read(byteCode, 0, (int)resourceD!.Length);
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate callee code");
            var from = keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair, true);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlock(2);

            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed callee contract");
            var calleeAddress = contractHash;
            
            // Deploy caller contract 
            if(resourceC is null)
                Assert.Fail("Failed to read script from resources");
            byteCode = new byte[resourceC!.Length];
            resourceC!.Read(byteCode, 0, (int)resourceC!.Length);
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate caller code");
            nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            tx = _transactionBuilder.DeployTransaction(from, byteCode);
            signedTx = Signer.Sign(tx, keyPair, true);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlock(3);
            
            // check contract is deployed
            contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed caller contract");
            
            // init caller contract 
            tx = _transactionBuilder.InvokeTransaction(from, contractHash, Money.Zero, "init(address)", calleeAddress);
            signedTx = Signer.Sign(tx, keyPair, true);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlock(4);

            // invoke caller contract 
            var transactionReceipt = new TransactionReceipt();
            transactionReceipt.Transaction = new Transaction();
            transactionReceipt.Transaction.Value = 0.ToUInt256();
            var abi = ContractEncoder.Encode("getA()");
            var invocationResult = VirtualMachine.InvokeWasmContract(
                contract!,
                new InvocationContext(from, _stateManager.LastApprovedSnapshot, transactionReceipt),
                abi,
                GasMetering.DefaultBlockGasLimit
            );
            Assert.That(invocationResult.Status == ExecutionStatus.Ok, "Failed to invoke caller contract");
            Assert.That(invocationResult.GasUsed > 0, "No gas used during contract invocation");
            Assert.AreEqual(invocationResult.ReturnValue!.ToHex(), "0x0000000000000000000000000000000000000000000000000000000000000064", 
                "Invalid invocation return value");
        }

        [Test]
        public void Test_ContractEvents()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.Event.wasm");
            if(resource is null) 
                Assert.Fail("Failed t read script from resources");
            var keyPair = _wallet.EcdsaKeyPair;
            GenerateBlock(1);
            //
            // // Deploy contract 
            // var byteCode = new byte[resource!.Length];
            // resource!.Read(byteCode, 0, (int)resource!.Length);
            // Assert.That(VirtualMachine.VerifyContract(byteCode), "Unable to validate contract code");
            // var from = keyPair.PublicKey.GetAddress();
            // var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            // var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            // var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            // var signedTx = Signer.Sign(tx, keyPair);
            // Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            // GenerateBlocks(1);
            //
            // // check contract is deployed
            // var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            // Assert.That(contract != null, "Failed to find deployed contract");
            //
            // // invoke contract 
            // var txInvoke = _transactionBuilder.InvokeTransaction(
            //     keyPair.PublicKey.GetAddress(),
            //     contractHash,
            //     Money.Zero,
            //     "test()",
            //     new dynamic[0]);
            // var signedTxInvoke = Signer.Sign(txInvoke, keyPair);
            // var error = _transactionPool.Add(signedTxInvoke);
            // Assert.That(error == OperatingError.Ok, "Failed to add invoke tx to pool");
            // GenerateBlocks(1);
            // var tx2 = 
            //     _stateManager.CurrentSnapshot.Transactions.GetTransactionByHash(signedTxInvoke.Hash);
            // Assert.That(tx2 != null, "Failed to add invoke tx to block");
            // Assert.That(tx2!.Status == TransactionStatus.Executed, "Failed to execute invoke tx");
            // Assert.That(tx2.GasUsed > 0, "Invoke tx didn't use gas");
            //
            // var emitedEvent =
            //     _stateManager.CurrentSnapshot.Events.GetEventByTransactionHashAndIndex(signedTxInvoke.Hash, 0);
            // Assert.NotNull(emitedEvent);
            // Assert.That(emitedEvent!.TransactionHash.Equals(signedTxInvoke.Hash), "Invalid tx hash in event");
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
                keyPair.PrivateKey.Encode(), true
            ).ToSignature(true);

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