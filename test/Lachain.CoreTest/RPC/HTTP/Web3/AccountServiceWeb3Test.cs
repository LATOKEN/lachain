using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Core.DI;
using Lachain.Storage.State;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Vault;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.CLI;
using System.IO;
using Lachain.Core.Config;
using AustinHarris.JsonRpc;
using Lachain.Storage.Repositories;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Crypto.Misc;
using Lachain.Core.Blockchain.Pool;
using Lachain.Networking;
using Nethereum.Signer;
using Lachain.Utility.Serialization;
using Transaction = Lachain.Proto.Transaction;
using Lachain.Crypto.ECDSA;


namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class AccountServiceWeb3Test
    {

        private IConfigManager _configManager = null!;
        private IContainer? _container;
        private IStateManager _stateManager = null!;
        private ISnapshotIndexRepository _snapshotIndexer = null!;
        private IContractRegisterer _contractRegisterer = null!;
        private IPrivateWallet _privateWallet = null!;
        private ISystemContractReader _systemContractReader = null!;
        private ITransactionPool _transactionPool = null!;
        private ITransactionManager _transactionManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;
        private ITransactionSigner _transactionSigner = null!;
        private AccountServiceWeb3 _apiService = null!;
        private TransactionServiceWeb3 _transactionApiService = null!;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IBlockManager _blockManager = null!;

        private static readonly string ByteCodeHex = "0061736D01000000011C0660017F006000017F60037F7F7F0060027F7F0060017F017F60000002610503656E760C6765745F6D736776616C7565000003656E760D6765745F636F64655F73697A65000103656E760F636F70795F636F64655F76616C7565000203656E760A7365745F72657475726E000303656E760B73797374656D5F68616C74000003040304050505030100020608017F01418080040B071202066D656D6F7279020005737461727400070ACE0203A60101047F418080042101024003400240200128020C0D002001280208220220004F0D020B200128020022010D000B41002101410028020821020B02402002200041076A41787122036B22024118490D00200120036A41106A22002001280200220436020002402004450D00200420003602040B2000200241706A3602082000410036020C2000200136020420012000360200200120033602080B2001410136020C200141106A0B2E004100410036028080044100410036028480044100410036028C800441003F0041107441F0FF7B6A36028880040B7501027F230041206B220024002000100002402000290300200041106A29030084200041086A290300200041186A29030084844200520D0010064100100141D5726A220036029007410020001005220136029407200141AB0D200010024100418F07100341001004000B41004100100341011004000B0B9607010041000B8F070061736D01000000011C0660017F006000017F60037F7F7F0060027F7F0060017F017F60000002610503656E760C6765745F6D736776616C7565000003656E760D6765745F63616C6C5F73697A65000103656E760F636F70795F63616C6C5F76616C7565000203656E760A7365745F72657475726E000303656E760B73797374656D5F68616C7400000305040304050505030100020608017F01418080040B071202066D656D6F7279020005737461727400080ACC0304240002402001450D00034020004200370300200041086A21002001417F6A22010D000B0B0BA60101047F418080042101024003400240200128020C0D002001280208220220004F0D020B200128020022010D000B41002101410028020821020B02402002200041076A41787122036B22024118490D00200120036A41106A22002001280200220436020002402004450D00200420003602040B2000200241706A3602082000410036020C2000200136020420012000360200200120033602080B2001410136020C200141106A0B2E004100410036028080044100410036028480044100410036028C800441003F0041107441F0FF7B6A36028880040BCD0101037F230041306B22002400200041086A10000240024002402000290308200041186A29030084200041106A290300200041206A29030084844200520D00100741001001220136020441002001100622023602084100200120021002200141034D0D01410020022802002201360200200141F8D1F6EF06470D012000412A3A002F4100450D0241004100100341011004000B41004100100341011004000B41004100100341011004000B412010062201410410052001411F6A20002D002F3A000020014120100341001004000B007D0970726F647563657273010C70726F6365737365642D62790105636C616E675D31322E302E31202868747470733A2F2F6769746875622E636F6D2F736F6C616E612D6C6162732F6C6C766D2D70726F6A656374203734373435346230616535353664613132306239616431363435613133653438633430363031613629008B01046E616D65017009000C6765745F6D736776616C7565010D6765745F63616C6C5F73697A65020F636F70795F63616C6C5F76616C7565030A7365745F72657475726E040B73797374656D5F68616C7405085F5F627A65726F3806085F5F6D616C6C6F63070B5F5F696E69745F6865617008057374617274071201000F5F5F737461636B5F706F696E746572007D0970726F647563657273010C70726F6365737365642D62790105636C616E675D31322E302E31202868747470733A2F2F6769746875622E636F6D2F736F6C616E612D6C6162732F6C6C766D2D70726F6A656374203734373435346230616535353664613132306239616431363435613133653438633430363031613629008D01046E616D65016608000C6765745F6D736776616C7565010D6765745F636F64655F73697A65020F636F70795F636F64655F76616C7565030A7365745F72657475726E040B73797374656D5F68616C7405085F5F6D616C6C6F63060B5F5F696E69745F6865617007057374617274071201000F5F5F737461636B5F706F696E746572090A0100072E726F64617461";


        [SetUp]
        public void Setup()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();


            _configManager = _container.Resolve<IConfigManager>();
            _stateManager = _container.Resolve<IStateManager>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _snapshotIndexer = _container.Resolve<ISnapshotIndexRepository>();
            _systemContractReader = _container.Resolve<ISystemContractReader>();

            _transactionManager = _container.Resolve<ITransactionManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _blockManager = _container.Resolve<IBlockManager>();

            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }
            ServiceBinder.BindService<GenericParameterAttributes>();

            _apiService = new AccountServiceWeb3(_stateManager, _snapshotIndexer, _contractRegisterer, _systemContractReader, _transactionPool);

            _transactionApiService = new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionBuilder, _transactionSigner,
                _transactionPool, _contractRegisterer, _privateWallet, _blockManager);
            
            _blockManager.TryBuildGenesisBlock();

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
        // Changed GetBalance to public
        public void TestGetBalance()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var bal = _apiService.GetBalance(address, "latest");
            Assert.AreEqual(bal, "0x84595161401484a000000");

            //updating balance
            _stateManager.LastApprovedSnapshot.Balances.SetBalance(address.HexToBytes().ToUInt160(), Money.Parse("90000000000000000"));
            var balNew = _apiService.GetBalance(address, "latest");

            Assert.AreEqual(balNew, "0x115557b419c5c1f3fa018400000000");
        }

        [Test]
        // Changed GetBalance to public
        public void TestGetBalancePending()
        {

            var tx = TestUtils.GetRandomTransaction(false);
            _stateManager.LastApprovedSnapshot.Balances.AddBalance(tx.Transaction.From, Money.Parse("1000"));
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);

            var beforeBalance = _stateManager.LastApprovedSnapshot.Balances.GetBalance(tx.Transaction.From);
            var sentValue = new Money(tx.Transaction.Value);
            var afterBalance = beforeBalance - sentValue - new Money(new BigInteger(tx.Transaction.GasLimit) * tx.Transaction.GasPrice);
            var knownAfterBalance = Money.Parse("1000") - sentValue - new Money(new BigInteger(tx.Transaction.GasLimit) * tx.Transaction.GasPrice);
            Assert.AreEqual(knownAfterBalance, afterBalance);
            var balance = _apiService.GetBalance(tx.Transaction.From.ToHex(), "pending");
            Assert.IsTrue(Web3DataFormatUtils.Web3Number(afterBalance.ToWei().ToUInt256()) == balance);

        }

        [Test]
        public void TestGetAccounts()
        {
            var accountList = _apiService.GetAccounts();
            var address = accountList.First!.ToString();

            var balances = _configManager!.GetConfig<GenesisConfig>("genesis")?.Balances;
            var balanceList = balances!.ToList();

            Assert.AreEqual(address, balanceList[1].Key);
        }

        [Test]
        // Changed GetTransactionCount to public
        public void Test_GetTransactionCount_latest()
        {
            _blockManager.TryBuildGenesisBlock();
            var tx = TestUtils.GetRandomTransaction(HardforkHeights.IsHardfork_9Active(1));
            // adding balance so that it's transaction is added to the pool
            _stateManager.LastApprovedSnapshot.Balances.AddBalance(tx.Transaction.From, Money.Parse("1000"));



            var txCountBefore = _apiService!.GetTransactionCount(tx.Transaction.From.ToHex(), "latest").HexToUlong();        


            Assert.AreEqual(txCountBefore, 0);
            
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);


            GenerateBlocks(1);


            var txCountAfter = _apiService!.GetTransactionCount(tx.Transaction.From.ToHex(), "latest").HexToUlong();

            Assert.AreEqual(txCountAfter, 1);
        }

        [Test]
        // Changed GetTransactionCount to public
        public void Test_GetTransactionCount_pending()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx2 = MakeDummyTx(HardforkHeights.IsHardfork_9Active(1));

            var ethTx = new LegacyTransactionChainId(rawTx2.HexToBytes());
            var address = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();


            var txCountBefore = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "pending").HexToUlong();

            Assert.AreEqual(txCountBefore, 0);

            //TransactionUtils.SetChainId(41);
            ExecuteDummyTransaction(false, rawTx2);

            var txCountAfter = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "pending").HexToUlong();

            Assert.AreEqual(txCountAfter, 1);
        }

        [Test]
        // Changed GetTransactionCount to public
        public void Test_GetTransactionCount_blockId()
        {

            _blockManager.TryBuildGenesisBlock();

            var rawTx2 = MakeDummyTx(HardforkHeights.IsHardfork_9Active(1));

            var ethTx = new LegacyTransactionChainId(rawTx2.HexToBytes());
            var address = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();


            var txCountBefore = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "latest").HexToUlong();

            Assert.AreEqual(txCountBefore, 0);

            //TransactionUtils.SetChainId(41);
            ExecuteDummyTransaction(true, rawTx2);

            var txCountAfter = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "0x1").HexToUlong();

            Assert.AreEqual(txCountAfter, 1);
        }

        [Test]
        [Ignore("TODO: fix it")]
        public void Test_GetCode_pending()
        {
            var keyPair = _privateWallet.EcdsaKeyPair;
            var address = "0x9210567c1f79e9e9c3634331158d3143e572c001";

            //var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
            //TransactionUtils.SetChainId((int)chainId!);

            // Deploy contract 
            var byteCode = ByteCodeHex.HexToBytes();
            //Assert.That(VirtualMachine.VerifyContract(byteCode, true), "Unable to validate smart-contract code");
            var from = keyPair.PublicKey.GetAddress();
            var nonce = _stateManager.LastApprovedSnapshot.Transactions.GetTotalTransactionCount(from);
            var contractHash = from.ToBytes().Concat(nonce.ToBytes()).Ripemd();
            var tx = _transactionBuilder.DeployTransaction(from, byteCode);

            var signedTx = _transactionSigner.Sign(tx, keyPair, true);
            var result = _transactionPool.Add(signedTx);

            var adCode = _apiService!.GetCode(address, "pending");
            var adCode_bytes = adCode.HexToBytes();
            Assert.AreEqual(adCode_bytes, byteCode);
        }

        [Test]
        // Changed GetCode to public
        public void Test_GetCode_earliest()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx2 = MakeDummyTx(HardforkHeights.IsHardfork_9Active(1));

            ExecuteDummyTransaction(true, rawTx2);

            var address = "0x9210567c1f79e9e9c3634331158d3143e572c001";

            var adCode = _apiService!.GetCode(address, "earliest");
            Assert.AreEqual(adCode, "");
        }

        [Test]
        // Changed GetCode to public
        public void Test_GetCode_latest()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var adCode = _apiService!.GetCode(address, "latest");
            Assert.AreEqual(adCode, "");
        }

        [Test]
        // Changed GetCode to public
        public void Test_GetCode_blockId()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx2 = MakeDummyTx(HardforkHeights.IsHardfork_9Active(1));

            ExecuteDummyTransaction(true, rawTx2);

            var address = "0x9210567c1f79e9e9c3634331158d3143e572c001";

            var adCode = _apiService!.GetCode(address, "0x1");
            Assert.AreEqual(adCode, "");
        }

        private string MakeDummyTx(bool useNewCainId)
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

            var rlp = tx.Rlp(useNewCainId);

            var keyPair = new EcdsaKeyPair("0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48"
                .HexToBytes().ToPrivateKey());
            var receipt = _transactionSigner.Sign(tx, keyPair, useNewCainId);

            var s = receipt.Signature;
            var rawTx = tx.RlpWithSignature(s, useNewCainId);

            return rawTx.ToHex();

        }

        // Below methods Execute a Transaction
        private void ExecuteDummyTransaction(bool generateblock, string rawTx)
        {
            _blockManager.TryBuildGenesisBlock();

            var ethTx = new LegacyTransactionChainId(rawTx.HexToBytes());

            var txHashSent = _transactionApiService.SendRawTransaction(rawTx);

            var sender = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            // Updating balance of sender's Wallet
            _stateManager.LastApprovedSnapshot.Balances.SetBalance(sender, Money.Parse("90000000000000000"));

            if (generateblock)
            {
                GenerateBlocks(1);
            }

        }

        private void GenerateBlocks(ulong blockNum)
        {
            for (ulong i = 1; i <= blockNum; i++)
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