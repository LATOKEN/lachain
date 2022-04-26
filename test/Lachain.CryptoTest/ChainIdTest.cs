using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using NUnit.Framework;
using Lachain.Crypto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Google.Protobuf;
using Secp256k1Net;
using Lachain.Logger;

namespace Lachain.CryptoTest
{
    public class ChainIdTest
    {
        private static readonly ILogger<ChainIdTest> Logger = LoggerFactory.GetLoggerForClass<ChainIdTest>();
        private static readonly byte[] TestString =
            Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");
        private static readonly Secp256k1 Secp256K1 = new Secp256k1();


        [SetUp]
        public void Setup()
        {
        }

        [TearDown]
        public void Teardown()
        {
            TransactionUtils.Dispose();
        }


        [Test]
        [Repeat(2)]
        public void Test_SignatureWithMultipleChainId()
        {
            var privateKey = "0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes();
            var ecsdsaPrivateKey = new ECDSAPrivateKey
            {
                Buffer = ByteString.CopyFrom(privateKey)
            };
            var key = new EcdsaKeyPair(ecsdsaPrivateKey);
            var pk = new byte[64];
            var publicKey = key.PublicKey.Buffer.ToByteArray();
            Assert.AreEqual(true, Secp256K1.PublicKeyParse(pk, publicKey));
            var publicKeySerialized = new byte[33];
            Assert.IsTrue(Secp256K1.PublicKeySerialize(publicKeySerialized, pk, Flags.SECP256K1_EC_COMPRESSED));
            Assert.AreEqual(publicKey, publicKeySerialized);

            var chainIds = new List<(int,int)>();
            // Add some chain id to test
            int total = 10;
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            for (int i = 0 ; i < total ; i++)
            {
                chainIds.Add(GetRandomChainId(25, 109, rng));
            }

            foreach (var (oldChainId, newChainId) in chainIds)
            {
                Logger.LogInformation($"old chain id: {oldChainId}, new chain id: {newChainId}");
                SetChainId(oldChainId, newChainId);
                SignMessageAndVerify(key);
            }
        }

        public void SignMessageAndVerify(EcdsaKeyPair key)
        {
            var crypto = CryptoProvider.GetCrypto();
            byte[] random = new byte[32];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            var publicKey = key.PublicKey.Buffer.ToByteArray();
            int n = 1000;
            for (var it = 0; it < n; ++it)
            {
                rng.GetBytes(random);
                var message = random.ToHex()
                    + "ec808504a817c800825208948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080"
                    + it.ToHex().Substring(2);
                rng.GetBytes(random);
                message += random.ToHex().Substring(2);
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, key.PrivateKey.Buffer.ToByteArray(), true);
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey, true));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature, true);
                Assert.AreEqual(recoveredPubkey, publicKey);
            }
        }

        public (int,int) GetRandomChainId(int lowestChainId, int highestChainId, RNGCryptoServiceProvider rng)
        {
            var range = highestChainId - lowestChainId;
            return (GetRandomByteInRange(range, rng) , GetRandomByteInRange(range, rng));
        }

        public int GetRandomByteInRange(int range, RNGCryptoServiceProvider rng)
        {
            Assert.That(range > 0);
            var random = new byte[1];
            rng.GetBytes(random);
            return random[0] % range;
        }

        private void SetChainId(int oldChainId, int newChainId)
        {
            TransactionUtils.Dispose();
            TransactionUtils.SetChainId(oldChainId, newChainId);
        }
    }
}