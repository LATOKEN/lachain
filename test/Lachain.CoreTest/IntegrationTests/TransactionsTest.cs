using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Storage.State;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.Misc;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

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

            // this is correct RLP of unsigned ethereum tx with chain id 25, check at https://toolkit.abdk.consulting/ethereum#transaction
            var expectedRawHash =
                "0xef8085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d0000080198080"
                    .HexToBytes()
                    .Keccak();
            Console.WriteLine($"rlp: {tx.Rlp(true).ToHex()}");
            Assert.AreEqual(expectedRawHash, tx.RawHash(false));
            Assert.AreEqual(expectedRawHash, tx.RawHash(true));
            
            // using old chain id
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

            // using new chain id
            // this is correct RLP of signed ethereum tx with chain id 225, check at https://toolkit.abdk.consulting/ethereum#transaction
            // signature is deterministic in compliance with https://tools.ietf.org/html/rfc6979
            expectedFullHash = 
                "0xf8718085174876e8008405f5e10094b8cd3195faf7da8a87a2816b9b4bba2a19d25dab8901158e460913d00000808201e5a0c763aabd0587adb01786db24eac39c76a22bfcf97d19ad07f3b64ddd2294bdeba04710bc793cd49e4a957cc4babec18c27eabd8c873267b1fdb4943f16de22321f"
                    .HexToBytes()
                    .Keccak();
            receipt = signer.Sign(tx, keyPair, true);
            Assert.AreEqual(
                expectedFullHash,
                receipt.Hash
            );

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
            tx3.Transaction.Nonce++;
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