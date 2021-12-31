using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using Nethereum.Signer;
using NUnit.Framework;

using Lachain.Core.Blockchain.Operations;
using Lachain.Crypto.Misc;
using Lachain.Utility;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Google.Protobuf;
using System.Collections.Generic;
using Lachain.Networking;
using Newtonsoft.Json.Linq;
using Lachain.Utility.Serialization;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3TransactionTests
    {
        private IContainer? _container;
        private IStateManager? _stateManager;
        private ITransactionManager? _transactionManager;
        private ITransactionBuilder? _transactionBuilder;
        private ITransactionSigner? _transactionSigner;
        private ITransactionPool? _transactionPool;
        private IContractRegisterer? _contractRegisterer;
        private IPrivateWallet? _privateWallet;
        private IConfigManager _configManager = null!;

        private TransactionServiceWeb3? _apiService;

        // from BlockTest.cs
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new Core.Blockchain.Operations.TransactionSigner();
        private IBlockManager _blockManager = null!;
        //private IPrivateWallet _privateWallet = null!;

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

            _stateManager = _container.Resolve<IStateManager>();
            _transactionManager = _container.Resolve<ITransactionManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            
            ServiceBinder.BindService<GenericParameterAttributes>();

            _apiService = new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionBuilder, _transactionSigner,
                _transactionPool, _contractRegisterer, _privateWallet);

            // from BlockTest.cs
            _blockManager = _container.Resolve<IBlockManager>();
            _configManager = _container.Resolve<IConfigManager>();
            //_privateWallet = _container.Resolve<IPrivateWallet>();
            // set chainId from config
            if (TransactionUtils.ChainId == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!);
            }

        }

        [TearDown]
        public void Teardown()
        {
            var sessionId = Handler.DefaultSessionId();
            Handler.DestroySession(sessionId);

            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }
        
        
        [Test]
        public void Test_SendRawTransactionSimpleSend()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx1 = MakeDummyTx();

            var ethTx = new TransactionChainId(rawTx1.HexToBytes());
            var t = _apiService!.MakeTransaction(ethTx);

            var txid = _apiService!.SendRawTransaction(rawTx1);
            Assert.AreEqual("0x", txid.Substring(0, 2));
            Assert.AreNotEqual("0x", txid);
        }
        

        [Test]
        [Ignore("fix it")]
        public void Test_SendRawTransactionContractInvocation()
        {
            var rawTx2 = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var ethTx = new TransactionChainId(rawTx2.HexToBytes());

            var t = _apiService!.MakeTransaction(ethTx);
            
            var r = ethTx.Signature.R;
            while (r.Length < 32)
                r = "00".HexToBytes().Concat(r).ToArray();
            
            var s = ethTx.Signature.S;
            while (s.Length < 32)
                s = "00".HexToBytes().Concat(s).ToArray();
            
            var signature = r.Concat(s).Concat(ethTx.Signature.V).ToArray();
            
            var keyPair = new EcdsaKeyPair("0xE83385AF76B2B1997326B567461FB73DD9C27EAB9E1E86D26779F4650C5F2B75"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(t, keyPair);
            Assert.AreEqual(receipt.Signature, signature.ToSignature());

            var ethTx2 = t.GetEthTx(receipt.Signature);
            Assert.AreEqual(ethTx.ChainId,  ethTx2.ChainId);
            Assert.AreEqual(ethTx.Data,  ethTx2.Data);
//            Assert.AreEqual(ethTx.Nonce,  ethTx2.Nonce);
            Assert.AreEqual(ethTx.Signature.R,  ethTx2.Signature.R);
            Assert.AreEqual(ethTx.Signature.S,  ethTx2.Signature.S);
            Assert.AreEqual(ethTx.Signature.V,  ethTx2.Signature.V);
//            Assert.AreEqual(ethTx.Value,  ethTx2.Value);
            Assert.AreEqual(ethTx.GasLimit,  ethTx2.GasLimit);
            Assert.AreEqual(ethTx.GasPrice,  ethTx2.GasPrice);
            Assert.AreEqual(ethTx.ReceiveAddress,  ethTx2.ReceiveAddress);
            //Assert.AreEqual(ethTx,  ethTx2);
            
            var txid = _apiService!.SendRawTransaction(rawTx2);
            // check we get a transaction hash,  not error message
            Assert.AreEqual("0x", txid.Substring(0, 2));
            // check this hash is not empty
            Assert.AreNotEqual("0x", txid);
            
            // check encoding is correct
            var decoder = new ContractDecoder(t.Invocation.ToByteArray());
            var args = decoder.Decode(Lrc20Interface.MethodSetAllowedSupply);
            var res = args[0] as UInt256 ?? throw new Exception("Failed to decode invocation");
            var supply = new BigInteger(5001000) * BigInteger.Pow(10, 18);;
            Console.WriteLine($"supply: {supply}");
            Assert.AreEqual(res.ToHex(), supply.ToUInt256().ToHex());
        }

        [Test]
        [Ignore("fix it")]
        public void Test_SendRawTransactionBatchParallel()
        {
            var rawTx = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b";

            var rawTx_arr = new List<string>();

            for (ulong i = 1000; i < 9000; i++)
            {
                var rawTx_new = rawTx + i.ToString();

                rawTx_arr.Add(rawTx_new);

                Console.WriteLine($"RawTx string {rawTx_new}\n");

            }


            var result = _apiService!.SendRawTransactionBatchParallel(rawTx_arr);

            Console.WriteLine($"Result len {result.Count()}\n");

        }

        [Test]
        public void Test_SendRawTransactionBatch()
        {
            var rawTx = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b";

            var rawTx_arr = new List<string>();

            for (ulong i = 1000; i < 9000; i++)
            {
                var rawTx_new = rawTx + i.ToString();

                rawTx_arr.Add(rawTx_new);

                Console.WriteLine($"RawTx string {rawTx_new}\n");

            }

            var result = _apiService!.SendRawTransactionBatch(rawTx_arr);

            Console.WriteLine($"Result len {result.Count()}\n");

        }

        [Test]
        [Ignore("fix it")]
        public void Test_VerifyRawTransaction_Valid_txn()
        {
            var rawTx = MakeDummyTx();

            var result = _apiService!.VerifyRawTransaction(rawTx);
            Assert.AreEqual(result, "0x2ad6261b4d33fc9d55eed4c48f16e33aba6178a8359c33237dba240b4f20aafb");
        }

        [Test]
        //changed GetTransactionReceipt from private to public
        public void Test_GetTransactionReceipt()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx = MakeDummyTx();

            var txHashSent = Execute_dummy_transaction(rawTx);
            Console.WriteLine($"tx sent: {txHashSent}");

            var txReceipt = _apiService!.GetTransactionReceipt(txHashSent);
            var txHashReceived = txReceipt["transactionHash"].ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());

        }

        [Test]
        //changed GetTransactionByHash from private to public
        public void Test_GetTransactionByHash()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx = MakeDummyTx();

            var txHashSent = Execute_dummy_transaction(rawTx);
            Console.WriteLine($"tx sent: {txHashSent}");

            var tx = _apiService!.GetTransactionByHash(txHashSent);
            var txHashReceived = tx["hash"].ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());

        }

        [Test]
        //changed GetTransactionByBlockHashAndIndex from private to public
        public void Test_GetTransactionByBlockHashAndIndex()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx = MakeDummyTx();

            var txHashSent = Execute_dummy_transaction(rawTx);
            Console.WriteLine($"tx sent: {txHashSent}");

            var tx = _apiService!.GetTransactionByHash(txHashSent);

            var blockHash = tx["blockHash"].ToString();
            var txIndex = (ulong)0; // 0

            var txFromBlockHash = _apiService!.GetTransactionByBlockHashAndIndex(blockHash, txIndex);
            var txHashReceived = txFromBlockHash["hash"].ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());


        }

        [Test]
        //changed GetTransactionByBlockNumberAndIndex from private to public
        public void Test_GetTransactionByBlockNumberAndIndex()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx = MakeDummyTx();

            var txHashSent = Execute_dummy_transaction(rawTx);
            Console.WriteLine($"tx sent: {txHashSent}");

            var tx = _apiService!.GetTransactionByHash(txHashSent);
            var txIndex = (ulong)0; // 0
            var txFromBlockHash = _apiService!.GetTransactionByBlockNumberAndIndex("latest", txIndex);
            var txHashReceived = txFromBlockHash["hash"];

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());

        }



        [Test]
        //changed InvokeContract from private to public
        public void Test_InvokeContract()
        {

        }

        [Test]
        //changed Call from private to public
        public void Test_Call()
        {
            _blockManager.TryBuildGenesisBlock();

            var keyPair = _privateWallet!.EcdsaKeyPair;
            GenerateBlocks(1);

            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var fromHx = from.ToHex();

            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var contractHashHx = contractHash.ToHex();

            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(1);

            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed contract");

            var abi = ContractEncoder.Encode("test()");
            var data = abi.ToHex();

            JObject opts = new JObject
            {
                ["from"] = fromHx,
                ["to"] = contractHashHx,
                ["data"] = data
            };

            var result = _apiService!.Call(opts, "latest");

            Assert.AreEqual(result, "0x000000000000000000000000000000000000000000000000000000000000002a");

        }

        [Test]
        //changed EstimateGas from private to public
        public void Test_EstimateGas()
        {
            _blockManager.TryBuildGenesisBlock();

            var keyPair = _privateWallet!.EcdsaKeyPair;
            GenerateBlocks(1);

            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var fromHx = from.ToHex();

            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var contractHashHx = contractHash.ToHex();

            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = Signer.Sign(tx, keyPair);
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(1);

            // check contract is deployed
            var contract = _stateManager.LastApprovedSnapshot.Contracts.GetContractByHash(contractHash);
            Assert.That(contract != null, "Failed to find deployed contract");

            var abi = ContractEncoder.Encode("test()");
            var data = abi.ToHex();

            JObject opts = new JObject
            {
                ["from"] = fromHx,
                ["to"] = contractHashHx,
                ["data"] = data
            };

            var result = _apiService!.EstimateGas(opts);
            Assert.AreEqual(result, "0x2e1d22");
        }

        [Test]
        /// Changed from private to public
        public void Test_GetNetworkGasPrice()
        {
            var gasPrice_Expected = "0x0";

            var gasPrice_Actual = _apiService!.GetNetworkGasPrice();

            Assert.AreEqual(gasPrice_Expected, gasPrice_Actual);

        }

        private string MakeDummyTx()
        {
            var tx = new Transaction
            {
                From = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160(),
                To = "0x71B293C2593d4Ff9b534b2e691f56c1D18c95a17".HexToBytes().ToUInt160(),
                Value = Money.Parse("100").ToUInt256(),
                Nonce = 0,
                GasPrice = 5000000000,
                GasLimit = 4500000
            };

            var rlp = tx.Rlp();

            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(tx, keyPair);

            var s = receipt.Signature;
            var rawTx = tx.RlpWithSignature(s);

            return rawTx.ToHex();

        }

        // Below methods Execute a Transaction
        private String Execute_dummy_transaction(String rawTx)
        {
            _blockManager.TryBuildGenesisBlock();

            var ethTx = new TransactionChainId(rawTx.HexToBytes());

            var txHashSent = _apiService!.SendRawTransaction(rawTx);

            var sender = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            // Updating balance of sender's Wallet
            _stateManager.LastApprovedSnapshot.Balances.SetBalance(sender, Money.Parse("90000000000000000"));

            GenerateBlocks(1);

            return txHashSent;

        }

        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 0; i < blockNum; i++)
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


        // from BlockTest.cs
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

            var keyPair = _privateWallet.EcdsaKeyPair;

            var headerSignature = Crypto.SignHashed(
                header.Keccak().ToBytes(),
                keyPair.PrivateKey.Encode()
            ).ToSignature();

            var multisig = new MultiSig
            {
                Quorum = 1,
                Validators = { _privateWallet.EcdsaKeyPair.PublicKey },
                Signatures =
                {
                    new MultiSig.Types.SignatureByValidator
                    {
                        Key = _privateWallet.EcdsaKeyPair.PublicKey,
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

    }
}