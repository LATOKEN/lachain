using System;
using System.Linq;
using System.Reflection;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Core.DI;
using Lachain.Storage.State;
using Lachain.Core.Blockchain.Interface;
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
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Crypto.Misc;
using Lachain.Core.Blockchain.Pool;
using Nethereum.Hex.HexTypes;
using Nethereum.Signer;
using Lachain.Crypto.ECDSA;
using Transaction = Lachain.Proto.Transaction;
using Lachain.Networking;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class AccountServiceWeb3Test
    {

        private IConfigManager? _configManager;
        private IContainer? _container;
        private IStateManager? _stateManager;
        private ISnapshotIndexRepository? _snapshotIndexer;
        private IContractRegisterer? _contractRegisterer;
        private IPrivateWallet? _privateWallet;
        private ISystemContractReader? _systemContractReader;
        private ITransactionPool? _transactionPool;

        private ITransactionManager? _transactionManager;
        private ITransactionBuilder? _transactionBuilder;
        private ITransactionSigner? _transactionSigner;

        private AccountServiceWeb3? _apiService;
        private TransactionServiceWeb3? _transaction_apiService;

        // from BlockTest.cs
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ITransactionSigner Signer = new Core.Blockchain.Operations.TransactionSigner();
        private IBlockManager _blockManager = null!;

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

            ServiceBinder.BindService<GenericParameterAttributes>();

            _apiService = new AccountServiceWeb3(_stateManager, _snapshotIndexer, _contractRegisterer, _systemContractReader, _transactionPool);

            _transaction_apiService = new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionBuilder, _transactionSigner,
                _transactionPool, _contractRegisterer, _privateWallet);

            // from BlockTest.cs
            _blockManager = _container.Resolve<IBlockManager>();

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
        // Changed GetBalance to public
        public void TestGetBalance()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var bal = _apiService.GetBalance(address, "latest");
            Assert.AreEqual(bal, "0x0");

            //updating balance
            _stateManager.LastApprovedSnapshot.Balances.SetBalance(address.HexToBytes().ToUInt160(), Money.Parse("90000000000000000"));
            var balNew = _apiService.GetBalance(address, "latest");

            Assert.AreEqual(balNew, "0x115557b419c5c1f3fa018400000000");
        }

        [Test]
        // Changed GetBalance to public
        public void TestGetBalancePending()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var rawTx2 = MakeDummyTx();

            ExecuteDummyTransaction(false, rawTx2);

            var bal = _apiService.GetBalance(address, "pending");
            Assert.AreEqual(bal, "0x115557b419c5bc87e2b3b5217dc000");

        }

        [Test]
        public void TestGetAccounts()
        {
            var account_list = _apiService!.GetAccounts();
            var address = account_list.First.ToString();
            
            var balances = _configManager!.GetConfig<GenesisConfig>("genesis")?.Balances;
            var balanceList = balances!.ToList();
            
            Assert.AreEqual(address, balanceList[1].Key);
        }

        [Test]
        // Changed GetTransactionCount to public
        public void TestGetTransactionCount()
        {
            _blockManager.TryBuildGenesisBlock();

            var rawTx2 = MakeDummyTx();

            var ethTx = new TransactionChainId(rawTx2.HexToBytes());
            var address = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            var txCountBefore = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "latest");        
            Assert.AreEqual(txCountBefore, 0);

            ExecuteDummyTransaction(true, rawTx2);

            var txCountAfter = _apiService!.GetTransactionCount(ethTx.Key.GetPublicAddress(), "latest");
            Assert.AreEqual(txCountAfter, 1);
        }

        [Test]
        // Changed GetCode to public
        public void TestGetCode()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var adCode = _apiService!.GetCode(address, "latest");
            Assert.AreEqual(adCode, "");
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
        private String ExecuteDummyTransaction(bool generate_block, String rawTx)
        {
            _blockManager.TryBuildGenesisBlock();

            var ethTx = new TransactionChainId(rawTx.HexToBytes());

            var txHashSent = _transaction_apiService!.SendRawTransaction(rawTx);

            var sender = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();

            // Updating balance of sender's Wallet
            _stateManager.LastApprovedSnapshot.Balances.SetBalance(sender, Money.Parse("90000000000000000"));

            if (generate_block) {
                GenerateBlocks(1);
            }

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