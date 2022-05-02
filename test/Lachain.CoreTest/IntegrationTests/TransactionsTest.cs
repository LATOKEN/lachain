using System;
using System.IO;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Storage.State;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
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
        private IConfigManager _configManager = null!;
        private ITransactionPool _transactionPool = null!;
        private IStateManager _stateManager = null!;
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
        }

        [Test]
        public void Test_Tx_Pool_Adding()
        {
            var tx = TestUtils.GetRandomTransaction(false);
            var result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.InsufficientBalance, result);

            _stateManager.LastApprovedSnapshot.Balances.AddBalance(tx.Transaction.From, Money.Parse("1000"));
            result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);

            result = _transactionPool.Add(tx);
            Assert.AreEqual(OperatingError.AlreadyExists, result);

            var tx2 = TestUtils.GetRandomTransaction(false);
            tx2.Transaction.Nonce++;
            result = _transactionPool.Add(tx2);
            Assert.AreEqual(OperatingError.InvalidNonce, result); 

            /* TODO: maybe we should fix this strange behaviour */
            var tx3 = TestUtils.GetRandomTransaction(false);
            tx3.Transaction.From = UInt160Utils.Zero;
            result = _transactionPool.Add(tx3);
            Assert.AreEqual(OperatingError.Ok, result);
        }
    }
}