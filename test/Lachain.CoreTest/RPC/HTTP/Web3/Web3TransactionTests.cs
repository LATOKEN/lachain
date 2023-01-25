using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
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
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3TransactionTests
    {
        private IContainer? _container;
        private IStateManager _stateManager = null!;
        private ITransactionManager _transactionManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionSigner _transactionSigner = null!;
        private ITransactionPool _transactionPool = null!;
        private IContractRegisterer _contractRegisterer = null!;
        private IPrivateWallet _privateWallet = null!;
        private IConfigManager _configManager = null!;
        private IStorageManager _storageManager = null!;

        private TransactionServiceWeb3 _apiService = null!;

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IBlockManager _blockManager = null!;

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
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();

            _stateManager = _container.Resolve<IStateManager>();
            _storageManager = _container.Resolve<IStorageManager>();
            _transactionManager = _container.Resolve<ITransactionManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _blockManager = _container.Resolve<IBlockManager>();
            _configManager = _container.Resolve<IConfigManager>();
            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }
            ServiceBinder.BindService<GenericParameterAttributes>();

            _apiService = new TransactionServiceWeb3(_storageManager, _stateManager, _transactionManager, _transactionBuilder, _transactionSigner,
                _transactionPool, _contractRegisterer, _privateWallet, _blockManager);
            

        }

        [TearDown]
        public void Teardown()
        {

            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var sessionId = Handler.GetSessionHandler().SessionId;
            if(sessionId != null) Handler.DestroySession(sessionId);
        }
        
        
        [Test]
        [Ignore("fix it")]
        public void Test_SendRawTransactionSimpleSend()
        {
            var rawTx2 = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var ethTx = new LegacyTransactionChainId(rawTx2.HexToBytes());
            var t = _apiService!.MakeTransaction(ethTx);
            
            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(t, keyPair, true);

            var txid = _apiService!.SendRawTransaction(rawTx2);
            Assert.AreEqual("0x", txid.Substring(0, 2));
            Assert.AreNotEqual("0x", txid);
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
        [Ignore("implement it")]
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
            var rawTx = "0xf8848001832e1a3094010000000000000000000000000000000000000080a4c76d99bd000000000000000000000000000000000000000000042300c0d3ae6a03a0000075a0f5e9683653d203dc22397b6c9e1e39adf8f6f5ad68c593ba0bb6c35c9cd4dbb8a0247a8b0618930c5c4abe178cbafb69c6d3ed62cfa6fa33f5c8c8147d096b0aa0";
            var result = _apiService!.VerifyRawTransaction(rawTx);
            Assert.AreEqual(result, "0x2ad6261b4d33fc9d55eed4c48f16e33aba6178a8359c33237dba240b4f20aafb");
        }

        [Test]
        //changed GetTransactionReceipt from private to public
        public void Test_GetTransactionReceipt()
        {
             _blockManager.TryBuildGenesisBlock();
            var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(1));
            _stateManager.LastApprovedSnapshot.Balances.MintLaToken(tx.Transaction.From, Money.Parse("1000"));
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);
            GenerateBlocks(1, 1);
            var txHashSent = tx.Hash.ToHex();
            var txReceipt = _apiService.GetTransactionReceipt(txHashSent);
            var txHashReceived = txReceipt!["transactionHash"]!.ToString();
            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());
        }

        [Test]
        //changed GetTransactionByHash from private to public
        public void Test_GetTransactionByHash()
        {
            _blockManager.TryBuildGenesisBlock();
            var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(1));
            _stateManager.LastApprovedSnapshot.Balances.MintLaToken(tx.Transaction.From, Money.Parse("1000"));
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);
            GenerateBlocks(1, 1);
            var txHashSent = tx.Hash.ToHex();

            var tx2 = _apiService!.GetTransactionByHash(txHashSent);
            var txHashReceived = tx2!["hash"]!.ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());

        }

        [Test]
        //changed GetTransactionByBlockHashAndIndex from private to public
        public void Test_GetTransactionByBlockHashAndIndex()
        {
            _blockManager.TryBuildGenesisBlock();
            var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(1));
            _stateManager.LastApprovedSnapshot.Balances.MintLaToken(tx.Transaction.From, Money.Parse("1000"));
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);
            GenerateBlocks(1, 1);

            var txHashSent = tx.Hash.ToHex();
            var tx2 = _apiService!.GetTransactionByHash(txHashSent);
            var blockHash = tx2!["blockHash"]!.ToString();
            var txIndex = (ulong)0; // 0
            var txFromBlockHash = _apiService!.GetTransactionByBlockHashAndIndex(blockHash, txIndex);
            var txHashReceived = txFromBlockHash!["hash"]!.ToString();

            Assert.AreEqual(txHashReceived.ToString(), txHashSent.ToString());


        }

        [Test]
        //changed GetTransactionByBlockNumberAndIndex from private to public
        public void Test_GetTransactionByBlockNumberAndIndex()
        {

            _blockManager.TryBuildGenesisBlock();
            var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(1));
            _stateManager.LastApprovedSnapshot.Balances.MintLaToken(tx.Transaction.From, Money.Parse("1000"));
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);
            GenerateBlocks(1, 1);

            var txHashSent = tx.Hash.ToHex();
            var txIndex = (ulong)0; // 0
            var txFromBlockHash = _apiService!.GetTransactionByBlockNumberAndIndex("latest", txIndex);
            var txHashReceived = txFromBlockHash!["hash"]!;

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
            GenerateBlocks(1, 1);

            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var fromHx = from.ToHex();

            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var contractHashHx = contractHash.ToHex();

            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = _transactionSigner.Sign(tx, keyPair, HardforkHeights.IsHardfork_9Active(2));
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(2, 2);

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

            
            // after hardfork:
            while (!HardforkHeights.IsHardfork_16Active(_blockManager.GetHeight()))
                GenerateBlocks(_blockManager.GetHeight() + 1, _blockManager.GetHeight() + 1);

            DeployAndTestContract("0x2e356e");

            var receipt = TestUtils.GetRandomTransaction(false);
            var opts = new JObject
            {
                ["from"] = receipt.Transaction.From.ToHex(),
                ["to"] = receipt.Transaction.To.ToHex(),
            };

            var result = _apiService!.EstimateGas(opts);
            Assert.AreNotEqual(result, "0x");
        }

        [Test]
        /// Changed from private to public
        [Ignore("fix it")]
        public void Test_GetNetworkGasPrice()
        {
            var gasPrice_Expected = "0x1";

            var gasPrice_Actual = _apiService!.GetNetworkGasPrice();

            Assert.AreEqual(gasPrice_Expected, gasPrice_Actual);

        }

        private void DeployAndTestContract(string gasEstimate)
        {
            // Deploy contract 
            var keyPair = _privateWallet!.EcdsaKeyPair;
            var byteCode = ByteCodeHex.HexToBytes();
            Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var fromHx = from.ToHex();

            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var contractHashHx = contractHash.ToHex();

            var tx = _transactionBuilder.DeployTransaction(from, byteCode);
            var signedTx = _transactionSigner.Sign(tx, keyPair, HardforkHeights.IsHardfork_9Active(2));
            Assert.That(_transactionPool.Add(signedTx) == OperatingError.Ok, "Can't add deploy tx to pool");
            GenerateBlocks(_blockManager.GetHeight() + 1, _blockManager.GetHeight() + 1);

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
            System.Console.WriteLine(HardforkHeights.IsHardfork_16Active(_blockManager.GetHeight()));
            Assert.AreEqual(result, gasEstimate);
        }

        // Below methods Execute a Transaction
        private String Execute_dummy_transaction(String rawTx)
        {
            _blockManager.TryBuildGenesisBlock();

            var ethTx = new LegacyTransactionChainId(rawTx.HexToBytes());

            var txHashSent = _apiService!.SendRawTransaction(rawTx);

            var sender = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            // Updating balance of sender's Wallet
            // _stateManager.LastApprovedSnapshot.Balances.SetBalance(sender, Money.Parse("90000000000000000"));

            GenerateBlocks(1, 1);

            return txHashSent;

        }

        private void GenerateBlocks(ulong from, ulong to)
        {
            for (ulong i = from; i <= to; i++)
            {
                var txes = GetCurrentPoolTxes(i);
                var block = BuildNextBlock(txes);
                var result = ExecuteBlock(block, txes);
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        private TransactionReceipt[] GetCurrentPoolTxes(ulong era)
        {
            return _transactionPool.Peek(1000, 1000, era).ToArray();
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
                keyPair.PrivateKey.Encode(), HardforkHeights.IsHardfork_9Active(blockIndex)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(blockIndex));

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
