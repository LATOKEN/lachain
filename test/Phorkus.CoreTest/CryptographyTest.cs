using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Asn1.Sec;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Core.Config;
using Phorkus.Core.Utils;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.CoreTest
{
    [TestClass]
    public class CryptographyTest
    {
        [TestMethod]
        public void Test_BouncyCastle_SignRoundTrip()
        {
            var crypto = new BouncyCastle();
            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);
            var address = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToBytes();

            CollectionAssert.AreEqual(address, crypto.ComputeAddress(publicKey));

            for (var it = 0; it < 256; ++it)
            {
                var message = "0xdeadbeef" + it.ToString("X2");
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, privateKey);
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature);
                CollectionAssert.AreEqual(recoveredPubkey, publicKey);
            }
        }

        [TestMethod]
        public void Test_BouncyCastle_Sign()
        {
            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var address = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToBytes();
            var publicKey =
                "0x04affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5529db79354bbace25f4f378d6a1320ae69994841ff6fb547f1b3a0c21cf73f68"
                    .HexToBytes();

            // Restored From Ethereum (0x6b address)
            //var PrivateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            //var Address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes();
            //var PublicKey ="0xe5974f3e1e9599ff5af036b5d6057d80855e7182afb4c2fa1fe38bc6efb9072b2c0b1382cc790ce4ad88c1d092d9432c63588fc089d56c522e6306dea55e1508".HexToBytes();

            var validators = new[]
            {
                "0x02103a7f7dd016558597f7960d27c516a4394fd968b9e65155eb4b013e4040406e"
            };
            var genesisAssetsBuilder = new GenesisAssetsBuilder(new ConfigManager("config.json"), new BouncyCastle());
            var genesisBuilder = new GenesisBuilder(genesisAssetsBuilder, new BouncyCastle(),
                new TransactionManager(null, null, null, null, null, null, new BouncyCastle()));

            var crypto = new BouncyCastle();

            var registerTx = genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction(null);
            var txManager = new TransactionManager(null, null, null, null, null, null, crypto);

            System.Console.Write("Signing transaction... ");
            var signed = txManager.Sign(registerTx, new KeyPair(privateKey.ToPrivateKey(), publicKey.ToPublicKey()));
            System.Console.WriteLine(signed.Signature.Buffer.ToHex());
            var publicKey2 = new PublicKey
            {
                Buffer = ByteString.CopyFrom(publicKey)
            };
            System.Console.Write("Verifing signature... ");
            var result = txManager.VerifySignature(signed, publicKey2);
            System.Console.WriteLine(result);

            signed.Signature = new Signature
            {
                Buffer = ByteString.CopyFrom(
                    "0x4a252a9a20db1fed75aaf5770857ee6d364ca7fbcefcd1dd7f3194aa59ae2cb1d22a3860900de65157ba625d80030ffdfc64d1cc872e94dc8aff43199fb6a7f5"
                        .HexToBytes())
            };
            var invalidSigned = new SignedTransaction
            {
                Transaction = signed.Transaction,
                Hash = signed.Transaction.ToHash256()
            };
            System.Console.Write("Verifing invalid signature... ");
            var result2 = txManager.VerifySignature(invalidSigned, publicKey2);
            System.Console.WriteLine(result2);
        }

        [TestMethod]
        public void Test_BouncyCastle_VerifySignature()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_RecoverSignature()
        {
            var crypto = new BouncyCastle();

            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey =
                "0x04affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5529db79354bbace25f4f378d6a1320ae69994841ff6fb547f1b3a0c21cf73f68"
                    .HexToBytes();
            var address = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToBytes();

            var address2 = crypto.ComputeAddress(publicKey);
            System.Console.WriteLine("Address: " + address2.ToHex());

            var message = "0xbadcab1e".HexToBytes().Sha256();
            var curve = SecNamedCurves.GetByName("secp256r1");
            var point = curve.Curve.DecodePoint(publicKey);
            System.Console.WriteLine("Compressed pubkey: " + point.GetEncoded(true).ToHex());

            var message2 = "0xbadcab1e".HexToBytes();
            var sig = crypto.Sign(message2, privateKey);

            System.Console.WriteLine("Signature: " + sig.ToHex());

            var isValid = crypto.VerifySignature(message, sig, publicKey);
            System.Console.WriteLine("Is signature valid: " + isValid);

            var publicKey2 = crypto.RecoverSignature(message, sig);
            System.Console.WriteLine("Restored public key: " + publicKey2.ToHex());
            System.Console.WriteLine("Restored address: " + crypto.ComputeAddress(publicKey2).ToHex());
        }

        [TestMethod]
        public void Test_BouncyCastle_ComputeAddress()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_ComputePublicKey()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_DecodePublicKey()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_AesEncrypt()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_AesDecrypt()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_SCrypt()
        {
        }

        [TestMethod]
        public void Test_BouncyCastle_GenerateRandomBytes()
        {
        }
    }
}