using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Lachain.CoreTest.IntegrationTests
{
    public class TransactionsTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IConfigManager _configManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
        private IBlockManager _blockManager = null!;
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
            _configManager = _container.Resolve<IConfigManager>();
            _transactionPool = _container.Resolve<ITransactionPool>();
            _stateManager = _container.Resolve<IStateManager>();
            _blockManager = _container.Resolve<IBlockManager>();
            _wallet = _container.Resolve<IPrivateWallet>();
            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }

        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_Tx_Building()
        {
            var signer = new TransactionSigner();
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());

            var tx = new Transaction
            {
                To = "0xB8CD3195faf7da8a87A2816B9b4bBA2A19D25dAb".HexToUInt160(),
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 100000000,
                Nonce = 0,
                Value = Money.Parse("20.0").ToUInt256()
            };
            Console.WriteLine($"Tx: from: {tx.From.ToHex()}, to: {tx.To.ToHex()}");

            // using old chain id
            // this is correct RLP of unsigned ethereum tx with chain id 25, check at https://toolkit.abdk.consulting/ethereum#transaction
            var expectedRawHash =
                "0xef8085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d0000080198080"
                    .HexToBytes()
                    .Keccak();
            Assert.AreEqual(expectedRawHash, tx.RawHash(false));
            
            // this is correct RLP of signed ethereum tx with chain id 25, check at https://toolkit.abdk.consulting/ethereum#transaction
            // signature is deterministic in compliance with https://tools.ietf.org/html/rfc6979
            var expectedFullHash =
                "0xf86f8085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d000008055a0c763aabd0587adb01786db24eac39c76a22bfcf97d19ad07f3b64ddd2294bdeba04710bc793cd49e4a957cc4babec18c27eabd8c873267b1fdb4943f16de22321f"
                    .HexToBytes()
                    .Keccak();
            var receipt = signer.Sign(tx, keyPair, false);
            Assert.AreEqual(
                expectedFullHash,
                receipt.Hash
            );
            var pubKey = receipt.RecoverPublicKey(false);
            Assert.AreEqual(receipt.Transaction.From.ToHex(), pubKey.GetAddress().ToHex());

            // using new chain id
            // this is correct RLP of unsigned ethereum tx with chain id 225, check at https://toolkit.abdk.consulting/ethereum#transaction
            expectedRawHash = 
                "0xf08085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d000008081e18080"
                    .HexToBytes()
                    .Keccak();
            Assert.AreEqual(expectedRawHash, tx.RawHash(true));
            
            // this is correct RLP of signed ethereum tx with chain id 225, check at https://toolkit.abdk.consulting/ethereum#transaction
            // signature is deterministic in compliance with https://tools.ietf.org/html/rfc6979
            expectedFullHash = 
                "0xf8718085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d00000808201e5a07daf59f5e3223da3732ff0f0ffc464c533a881245fe00d09573c792f8a5f055aa067da9775fd1253f05215f961a019b619cc4d2aa8e2ae6c29c8232152f6692c24"
                    .HexToBytes()
                    .Keccak();
            receipt = signer.Sign(tx, keyPair, true);
            Assert.AreEqual(
                expectedFullHash,
                receipt.Hash
            );
            pubKey = receipt.RecoverPublicKey(true);
            Assert.AreEqual(receipt.Transaction.From.ToHex(), pubKey.GetAddress().ToHex());

        }

        [Test]
        public void Test_Tx_Pool_Adding()
        {
            TestTxPoolAdding();
            _blockManager.TryBuildGenesisBlock();
            Assert.IsFalse(HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()));
            while(!HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight()))
            {
                var block = BuildNextBlock();
                var result = ExecuteBlock(block);
                Assert.AreEqual(OperatingError.Ok, result);
            }
            TestTxPoolAdding();
        }

        [Test]
        public void Test_Tx_Pool_Replacement()
        {
            _blockManager.TryBuildGenesisBlock();
            TestTxPoolAdding();
            
            var privateKey = Crypto.GeneratePrivateKey().ToPrivateKey();
            var keyPair = new EcdsaKeyPair(privateKey);
            AddRandomTxesToPool(keyPair, out var randomTxes);
            var rnd = new Random();
            randomTxes = randomTxes.OrderBy(item => rnd.Next()).ToList();
            bool useNewChainId = HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1);
            var signer = new TransactionSigner();
            foreach (var tx in randomTxes)
            {
                Console.WriteLine($"nonce: {tx.Transaction.Nonce}");
                var newTx = new Transaction(tx.Transaction);
                // lower price
                newTx.GasPrice = tx.Transaction.GasPrice - 1;
                var randomTx = TestUtils.GetRandomTransaction(useNewChainId).Transaction;
                newTx.To = randomTx.To;
                newTx.Value = randomTx.Value;
                newTx.GasLimit = randomTx.GasLimit;
                var newTxReceipt = signer.Sign(newTx, keyPair, useNewChainId);
                var result = _transactionPool.Add(newTxReceipt);
                Console.WriteLine($"old gas price: {tx.Transaction.GasPrice}, new gas price: {newTxReceipt.Transaction.GasPrice}");
                Assert.AreEqual(OperatingError.Underpriced, result);
                // equal price
                newTx.GasPrice = tx.Transaction.GasPrice;
                newTxReceipt = signer.Sign(newTx, keyPair, useNewChainId);
                result = _transactionPool.Add(newTxReceipt);
                Console.WriteLine($"old gas price: {tx.Transaction.GasPrice}, new gas price: {newTxReceipt.Transaction.GasPrice}");
                Assert.AreEqual(OperatingError.Underpriced, result);
                // higher price
                newTx.GasPrice = tx.Transaction.GasPrice + 1;
                newTxReceipt = signer.Sign(newTx, keyPair, useNewChainId);
                result = _transactionPool.Add(newTxReceipt);
                Console.WriteLine($"old gas price: {tx.Transaction.GasPrice}, new gas price: {newTxReceipt.Transaction.GasPrice}");
                Assert.AreEqual(OperatingError.DuplicatedTransaction, result);
                // higher price and all fields same
                newTx = new Transaction(tx.Transaction);
                newTx.GasPrice = tx.Transaction.GasPrice + 1;
                newTxReceipt = signer.Sign(newTx, keyPair, useNewChainId);
                result = _transactionPool.Add(newTxReceipt);
                Console.WriteLine($"old gas price: {tx.Transaction.GasPrice}, new gas price: {newTxReceipt.Transaction.GasPrice}");
                Assert.AreEqual(OperatingError.Ok, result);
            }
        }

        public void AddRandomTxesToPool(EcdsaKeyPair keyPair, out List<TransactionReceipt> txes)
        {
            bool useNewChainId = HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1);
            var txCount = 100;
            txes = new List<TransactionReceipt>();
            _stateManager.LastApprovedSnapshot.Balances.AddBalance(keyPair.PublicKey.GetAddress(), Money.Parse("1000"));
            for (var i = 0; i < txCount; i++)
            {
                var tx = TestUtils.GetRandomTransactionFromAddress(keyPair, (ulong)i, useNewChainId);
                var result = _transactionPool.Add(tx);
                Assert.AreEqual(OperatingError.Ok, result);
                txes.Add(tx);
            }
        }

        public void TestTxPoolAdding()
        {
            bool useNewChainId = HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1);
            var tx = TestUtils.GetRandomTransaction(useNewChainId);
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.InsufficientBalance, result);

            _stateManager.LastApprovedSnapshot.Balances.AddBalance(tx.Transaction.From, Money.Parse("1000"));
            result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);

            result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.AlreadyExists, result);

            var tx2 = TestUtils.GetRandomTransaction(useNewChainId);
            tx2.Transaction.Nonce++;
            result = _transactionPool.Add(tx2);
            Assert.AreEqual(OperatingError.InvalidNonce, result); 

            /* TODO: maybe we should fix this strange behaviour */
            var tx3 = TestUtils.GetRandomTransaction(useNewChainId);
            tx3.Transaction.From = UInt160Utils.Zero;
            tx3.Transaction.Nonce = _transactionPool.GetNextNonceForAddress(UInt160Utils.Zero) ;
            result = _transactionPool.Add(tx3);
            Assert.AreEqual(OperatingError.Ok, result);
        }

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
                keyPair.PrivateKey.Encode(), HardforkHeights.IsHardfork_9Active(blockIndex)
            ).ToSignature(HardforkHeights.IsHardfork_9Active(blockIndex));

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