using System;
using System.IO;
using System.Reflection;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Pool;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class TransactionsTest
    {
        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
        }

        [TearDown]
        public void Teardown()
        {
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

            var receipt = signer.Sign(tx, keyPair);

            Assert.AreEqual(
                "0xc1fdf2042d3e86986f38be1410c579ff16f5a0f1a72096d11beb48df1455060a".HexToUInt256(),
                receipt.Hash
            );
        }

        [Test]
        public void Test_Tx_Pool_Adding()
        {
            var containerBuilder = TestUtils.GetContainerBuilder(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json")
            );
            using var container = containerBuilder.Build();
            var txPool = container.Resolve<ITransactionPool>();

            var tx = TestUtils.GetRandomTransaction();
            var result = txPool.Add(tx);
            Assert.AreEqual(OperatingError.Ok, result);

            result = txPool.Add(tx);
            Assert.AreEqual(OperatingError.AlreadyExists, result);

            var tx2 = TestUtils.GetRandomTransaction();
            tx2.Transaction.Nonce++;
            result = txPool.Add(tx2);
            Assert.AreEqual(OperatingError.InvalidNonce, result);

            /* TODO: maybe we should fix this strange behaviour */
            var tx3 = TestUtils.GetRandomTransaction();
            tx3.Transaction.From = UInt160Utils.Zero;
            result = txPool.Add(tx3);
            Assert.AreEqual(OperatingError.Ok, result);
        }
    }
}