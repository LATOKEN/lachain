using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Org.BouncyCastle.Asn1.Sec;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Genesis;
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