using System;
using System.Linq;
using System.Text;
using Nethereum.Signer;
using NUnit.Framework;
using Lachain.Crypto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Prng;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.CryptoTest
{
    public class CryptographyTest
    {
        private static readonly byte[] TestString =
            Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");

        [Test]
        public void Test_KeccakTestVector()
        {
            Assert.AreEqual(
                "0x45d3b367a6904e6e8d502ee04999a7c27647f91fa845d456525fd352ae3d7371",
                TestString.KeccakBytes().ToHex()
            );
        }

        [Test]
        public void Test_RipemdTestVector()
        {
            Assert.AreEqual(
                "0x9c1185a5c5e9fc54612808977ee8f548b2258d31",
                Encoding.ASCII.GetBytes("").RipemdBytes().ToHex()
            );
            Assert.AreEqual(
                "0x12a053384a9c0c88e405a06c27dcf49ada62eb2b",
                TestString.RipemdBytes().ToHex()
            );
        }
        
        [Test]
        public void Test_Sha256TestVector()
        {
            Assert.AreEqual(
                "0xe3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
                Encoding.ASCII.GetBytes("").Sha256Bytes().ToHex()
            );
            Assert.AreEqual(
                "0x248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1",
                TestString.Sha256Bytes().ToHex()
            );
        }

        [Test]
        public void Test_KeccakPrngIsDeterministic()
        {
            var prng = new DigestRandomGenerator(new Sha3Digest());
            prng.AddSeedMaterial(new byte[] {0xde, 0xad, 0xbe, 0xef});
            var pseudoRandomBytes = new byte[32];
            prng.NextBytes(pseudoRandomBytes);
            Assert.AreEqual(
                "0x4439ed265e4aa7290429a9823b320ccd69dd39128b6fd9a04532d8782f26d70b",
                pseudoRandomBytes.ToHex()
            );
        }

        [Test]
        public void Test_AesGcmEncryptDecryptRoundTrip()
        {
            var crypto = CryptoProvider.GetCrypto();
            var key = crypto.GenerateRandomBytes(32);
            var baseText =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33"
                    .HexToBytes();
            const int n = 1000;
            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < n; ++it)
            {
                var plaintext = baseText.Concat(it.ToBytes()).ToArray();
                var cipher = crypto.AesGcmEncrypt(key, plaintext);
                var decrypted = crypto.AesGcmDecrypt(key, cipher);
                Assert.IsTrue(plaintext.SequenceEqual(decrypted));
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{n} encrypt/decrypt: {endTs - startTs}ms, avg = {(double) (endTs - startTs) / n}");
        }

        [Test]
        public void Test_Secp256K1EncryptDecryptRoundTrip()
        {
            var crypto = CryptoProvider.GetCrypto();
            var key = crypto.GeneratePrivateKey();
            var baseText =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33"
                    .HexToBytes();

            const int n = 1000;
            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < n; ++it)
            {
                var plaintext = baseText.Concat(it.ToBytes()).ToArray();
                var cipher = crypto.Secp256K1Encrypt(key.ToPrivateKey().GetPublicKey().EncodeCompressed(), plaintext);
                var decrypted = crypto.Secp256K1Decrypt(key, cipher);
                Assert.IsTrue(plaintext.SequenceEqual(decrypted));
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{n} encrypt/decrypt: {endTs - startTs}ms, avg = {(double) (endTs - startTs) / n}");
        }

        [Test]
        public void Test_SignRoundTrip()
        {
            var crypto = new DefaultCrypto();
            var privateKey = "0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);
            var address = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes();

            CollectionAssert.AreEqual(address, crypto.ComputeAddress(publicKey));

            var startTs = TimeUtils.CurrentTimeMillis();
            const int n = 100;
            for (var it = 0; it < n; ++it)
            {
                // var message = "0xdeadbeef" + it.ToString("X4");
                var message =
                    "0xec808504a817c800825208948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080";
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, privateKey);
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature);
                Assert.AreEqual(recoveredPubkey, publicKey);
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"Full sign + recover time: {endTs - startTs}ms");
            Console.WriteLine($"Per 1 iteration: {(double) (endTs - startTs) / n}ms");
        }

        [Test]
        public void Test_External_Signature()
        {
            var crypto = CryptoProvider.GetCrypto();
            /*
             * message is raw hash of eth transaction with following parameters
             * const txParams = {
             *   nonce: '0x00',
             *   gasPrice: '0x09184e72a000',
             *   gasLimit: '0x27100000',
             *   value: '0x00',
             *   data: '0x7f7465737432000000000000000000000000000000000000000000000000000000600057',
             * };
             * EIP-155 encoding used, with chain id = 41
             * Signature created from private key: 0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48
             * Address: 0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195
             */
            var message = "0x29aa471d6a7b7b4894b896791c3ed325fe3b70a7a0f07c2f8c3fef9d7cf0f7d2".HexToBytes();
            var signature =
                "0x8357f298e9d84c128e2a2f66727669bd096e55351ff13b3e6bab88ea810dd5643170a74d76efd85d1f57937aa1d3e1632e9198f7f94f0e94b79456a412b8927e76"
                    .HexToBytes();
            var pubKey = crypto.RecoverSignatureHashed(message, signature);
            Assert.AreEqual(
                "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower(),
                crypto.ComputeAddress(pubKey).ToHex().ToLower()
            );
        }

        [Test]
        public void Test_Rlp()
        {
            var tx = new Transaction
            {
                To = "0x5f193b130d7c856179aa3d738ee06fab65e73147".HexToBytes().ToUInt160(),
                Value = Money.Parse("100").ToUInt256(),
                Nonce = 0,
                GasPrice = 5000000000,
                GasLimit = 4500000
            };
            var rlp = tx.Rlp();
            // compare with actual RLP from metamask
            Assert.IsTrue(rlp.SequenceEqual(
                "0xee8085012a05f2008344aa20945f193b130d7c856179aa3d738ee06fab65e7314789056bc75e2d6310000080298080"
                    .HexToBytes()));
        }

        [Test]
        public void Test_External_Signature2()
        {
            var crypto = CryptoProvider.GetCrypto();

            var rawTx =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33";

            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            Console.WriteLine("ETH RLP: " + ethTx.GetRLPEncodedRaw().ToHex());

            var nonce = ethTx.Nonce.ToHex();

            Console.WriteLine("Nonce " + nonce);
            Console.WriteLine("ChainId " + Convert.ToUInt64(ethTx.ChainId.ToHex(), 16));

            var tx = new Transaction
            {
                To = ethTx.ReceiveAddress.ToUInt160(),
                Value = ethTx.Value.ToArray().ToUInt256(false, true),
                Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16)
            };

            Console.WriteLine("RLP: " + tx.Rlp().ToHex());

            var address = ethTx.Key.GetPublicAddress().HexToBytes();
            var from = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();
            Console.WriteLine(address.ToHex());

            var r = "0x115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78".HexToBytes();
            var s = "0x12fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33".HexToBytes();
            var v = "0x25".HexToBytes();
            var signature = r.Concat(s).Concat(v).ToArray();

            Console.WriteLine(signature.ToHex());

            var message =
                "0xed808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080"
                    .HexToBytes().KeccakBytes();

            var recoveredPubkey = crypto.RecoverSignatureHashed(message, signature);

            Console.WriteLine(recoveredPubkey.ToHex());

            var addr = crypto.ComputeAddress(recoveredPubkey);
            var same = addr.SequenceEqual(from.ToBytes());
            Console.WriteLine(addr.ToHex());
            Console.WriteLine(same);
        }
    }
}