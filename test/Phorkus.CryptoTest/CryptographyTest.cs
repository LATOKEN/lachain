using System;
using NUnit.Framework;
using Phorkus.Crypto;
using Phorkus.Utility.Utils;

namespace Phorkus.CryptoTest
{
    public class CryptographyTest
    {
        const int N = 1024;
        [Test]
        public void Test_BouncyCastle_SignRoundTrip()
        {
            var crypto = new BouncyCastle();
            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);
            var address = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes();
            
            CollectionAssert.AreEqual(address, crypto.ComputeAddress(publicKey));

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                var message = "0xdeadbeef" + it.ToString("X4");
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, privateKey);
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature);
                CollectionAssert.AreEqual(recoveredPubkey, publicKey);
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }
        
        [Test]
        public void Test_BouncyCastle_RecoverSignature()
        {
            var crypto = new BouncyCastle();

            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                var message = ("0xbadcab1e" + it.ToString("X4")).HexToBytes();
                var signature = crypto.Sign(message, privateKey);
                var recoveredPubkey = crypto.RecoverSignature(message, signature);
                CollectionAssert.AreEqual(recoveredPubkey.ToHex(), publicKey.ToHex());
            }
            
            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }
    }
}