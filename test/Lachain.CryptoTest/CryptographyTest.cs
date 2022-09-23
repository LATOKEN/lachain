using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Nethereum.Signer;
using NUnit.Framework;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Config;
using Lachain.Core.CLI;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Prng;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.CryptoTest
{
    public class CryptographyTest
    {
        private static readonly byte[] TestString =
            Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq");
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IContainer? _container;
        private IConfigManager _configManager = null!; 
        [SetUp]
        public void Setup()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();

            _container = containerBuilder.Build();

            _configManager = _container.Resolve<IConfigManager>();
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
            }

            Console.WriteLine($"old chain id: {TransactionUtils.ChainId(false)}, new chain id: {TransactionUtils.ChainId(true)}");
        }


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
        public void Test_HeaderKeccak()
        {
            var header = new BlockHeader
            {
                PrevBlockHash = UInt256Utils.Zero,
                MerkleRoot = UInt256Utils.Zero,
                Index = 0,
                StateHash = UInt256Utils.Zero,
                Nonce = 1
            };
            Console.WriteLine(HashUtils.Keccak(header).ToHex());
            Assert.AreEqual("0x3cf92ed8492413af89861763a3718cb9afd415ddb2bca10d2e8038cf2d1afb7f", HashUtils.Keccak(header).ToHex());
        }
 
        [Test]
        public void Test_AesGcmEncryptDecryptRoundTrip()
        {
            var key = Crypto.GenerateRandomBytes(32);
            var baseText =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33"
                    .HexToBytes();
            const int n = 1000;
            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < n; ++it)
            {
                var plaintext = baseText.Concat(it.ToBytes()).ToArray();
                var cipher = Crypto.AesGcmEncrypt(key, plaintext);
                var decrypted = Crypto.AesGcmDecrypt(key, cipher);
                Assert.IsTrue(plaintext.SequenceEqual(decrypted));
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{n} encrypt/decrypt: {endTs - startTs}ms, avg = {(double) (endTs - startTs) / n}");
        }

        [Test]
        public void Test_Secp256K1EncryptDecryptRoundTrip()
        {
            var key = Crypto.GeneratePrivateKey();
            var baseText =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33"
                    .HexToBytes();

            const int n = 1000;
            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < n; ++it)
            {
                var plaintext = baseText.Concat(it.ToBytes()).ToArray();
                var cipher = Crypto.Secp256K1Encrypt(key.ToPrivateKey().GetPublicKey().EncodeCompressed(), plaintext);
                var decrypted = Crypto.Secp256K1Decrypt(key, cipher);
                Assert.IsTrue(plaintext.SequenceEqual(decrypted));
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{n} encrypt/decrypt: {endTs - startTs}ms, avg = {(double) (endTs - startTs) / n}");
        }

        [Test]
        public void Test_SignRoundTrip()
        {
            var privateKey = "0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes();
            var publicKey = Crypto.ComputePublicKey(privateKey, true);
            var address = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes();

            CollectionAssert.AreEqual(address, Crypto.ComputeAddress(publicKey));

            var startTs = TimeUtils.CurrentTimeMillis();
            const int n = 100;
            for (var it = 0; it < n; ++it)
            {
                // var message = "0xdeadbeef" + it.ToString("X4");
                var message =
                    "0xec808504a817c800825208948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080";
                var digest = message.HexToBytes();
                // using old chain id
                var signature = Crypto.Sign(digest, privateKey, false);
                Assert.IsTrue(Crypto.VerifySignature(digest, signature, publicKey, false));
                var recoveredPubkey = Crypto.RecoverSignature(digest, signature, false);
                Assert.AreEqual(recoveredPubkey, publicKey);

                // using new chain id
                signature = Crypto.Sign(digest, privateKey, true);
                Assert.IsTrue(Crypto.VerifySignature(digest, signature, publicKey, true));
                recoveredPubkey = Crypto.RecoverSignature(digest, signature, true);
                Assert.AreEqual(recoveredPubkey, publicKey);
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"Full sign + recover time: {endTs - startTs}ms");
            Console.WriteLine($"Per 1 iteration: {(double) (endTs - startTs) / n}ms");
        }

        [Test]
        public void Test_TxHash()
        {
            var fromAddress = UInt160Utils.Zero;
            var tx = new Transaction
            {
                From = fromAddress,
                Nonce = (ulong) 0,
                Value = Money.Parse("10").ToUInt256(),
                To = "0x316fd97d8e47a2dec2c8783db740f08a10a13f4e".HexToUInt160(),
                GasPrice = 0,
            };
            Console.WriteLine($"Tx old full rlp is {tx.RlpWithSignature(SignatureUtils.ZeroOld, false).ToHex()}");
            Assert.AreEqual("0xe580000094316fd97d8e47a2dec2c8783db740f08a10a13f4e888ac7230489e8000080008080" ,
                tx.RlpWithSignature(SignatureUtils.ZeroOld, false).ToHex());
            Console.WriteLine($"Tx new full rlp is {tx.RlpWithSignature(SignatureUtils.ZeroNew, true).ToHex()}");
            Assert.AreEqual("0xe780000094316fd97d8e47a2dec2c8783db740f08a10a13f4e888ac7230489e80000808200008080" ,
                tx.RlpWithSignature(SignatureUtils.ZeroNew, true).ToHex());
            Console.WriteLine($"Tx old full hash is {tx.FullHash(SignatureUtils.ZeroOld, false).ToHex()}");
            Assert.AreEqual("0xe115bce0fbf8e927b1f3e4068c2b486281e5d298fdbbfa4b947c62329fc85abc" ,
                tx.FullHash(SignatureUtils.ZeroOld, false).ToHex());
            Console.WriteLine($"Tx new full hash is {tx.FullHash(SignatureUtils.ZeroNew, true).ToHex()}");
            Assert.AreEqual("0xad0a8811b4fc0c0f058678a2e0bf76f4d040b0214d6819be39a28dd414d97889" ,
                tx.FullHash(SignatureUtils.ZeroNew, true).ToHex());
        }


        // [Test]
        // public void Test_External_Signature()
        // {
        //     /*
        //      * message is raw hash of eth transaction with following parameters
        //      * const txParams = {
        //      *   nonce: '0x00',
        //      *   gasPrice: '0x09184e72a000',
        //      *   gasLimit: '0x27100000',
        //      *   value: '0x00',
        //      *   data: '0x7f7465737432000000000000000000000000000000000000000000000000000000600057',
        //      * };
        //      * EIP-155 encoding used, with chain id = 41
        //      * Signature created from private key: 0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48
        //      * Address: 0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195
        //      * RLP: 0xf7808609184e72a00084271000008080a47f7465737432000000000000000000000000000000000000000000000000000000600057298080
        //      * Check here: https://toolkit.abdk.consulting/ethereum#transaction
        //      */
        //     // 0xf7808609184e72a00084271000008080a47f7465737432000000000000000000000000000000000000000000000000000000600057198080
        //     var rlp = 
        //         "0xf7808609184e72a00084271000008080a47f7465737432000000000000000000000000000000000000000000000000000000600057298080"
        //             .HexToBytes();
        //     var rawHash = rlp.Keccak().ToBytes();
        //     var message = "0x29aa471d6a7b7b4894b896791c3ed325fe3b70a7a0f07c2f8c3fef9d7cf0f7d2".HexToBytes();
        //     Console.WriteLine($"message: {message.ToHex()}");
        //     Console.WriteLine($"raw hash: {rawHash.ToHex()}");
        //     Assert.AreEqual(rawHash, message);
        //     var signature =
        //         "0x8357f298e9d84c128e2a2f66727669bd096e55351ff13b3e6bab88ea810dd5643170a74d76efd85d1f57937aa1d3e1632e9198f7f94f0e94b79456a412b8927e76"
        //             .HexToBytes();
        //     var pubKey = Crypto.RecoverSignatureHashed(message, signature, false);
        //     Assert.AreEqual(
        //         "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower(),
        //         Crypto.ComputeAddress(pubKey).ToHex().ToLower()
        //     );
        // }


        [Test]
        public void Test_External_Signature()
        {
            // using old chain id
            /*
             * message is raw hash of eth transaction with following parameters
             * const txParams = {
             *   nonce: '0x00',
             *   gasPrice: '0x09184e72a000',
             *   gasLimit: '0x27100000',
             *   value: '0x00',
             *   data: '0x7f7465737432000000000000000000000000000000000000000000000000000000600057',
             * };
             * EIP-155 encoding used, with chain id = 25
             * Signature created from private key: 0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48
             * Address: 0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195
             * RLP: 0xf7808609184e72a00084271000008080a47f7465737432000000000000000000000000000000000000000000000000000000600057198080
             * Check here: https://toolkit.abdk.consulting/ethereum#transaction
             */

            var rlp = 
                "0xf7808609184e72a00084271000008080a47f7465737432000000000000000000000000000000000000000000000000000000600057198080"
                    .HexToBytes();
            var message = rlp.Keccak().ToBytes();

            // check the signature with python script
            var signature =
                "0xCC3D45ADFC4570ABA45FE873D17741434310F178B7AB406C11A2CD4E6AF8DCB727A3F460B3B7601425CFAEB7B89E734F637509F4691A5553547F638DBEB17BBB56"
                    .HexToBytes();
            var pubKey = Crypto.RecoverSignatureHashed(message, signature, false);
            Assert.AreEqual(
                "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower(),
                Crypto.ComputeAddress(pubKey).ToHex().ToLower()
            );

            // using new chain id
            /*
             * message is raw hash of eth transaction with following parameters
             * const txParams = {
             *   nonce: '0x00',
             *   gasPrice: '0x09184e72a000',
             *   gasLimit: '0x27100000',
             *   value: '0x00',
             *   data: '0x7f7465737432000000000000000000000000000000000000000000000000000000600057',
             * };
             * EIP-155 encoding used, with chain id = 225
             * Signature created from private key: 0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48
             * Address: 0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195
             * RLP: 0xf838808609184e72a00084271000008080a47f746573743200000000000000000000000000000000000000000000000000000060005781e18080
             * Check here: https://toolkit.abdk.consulting/ethereum#transaction
             */

            rlp = 
                "0xf838808609184e72a00084271000008080a47f746573743200000000000000000000000000000000000000000000000000000060005781e18080"
                    .HexToBytes();
            message = rlp.Keccak().ToBytes();

            // check the signature with python script
            signature =
                "0x3BBFC886AD74E518C99D59966B5B4B656D3ED03B71FACD9384A82D08C8ED825B170B6E685A3E15FA9969D729B6C036ED3E5C3926B6FC424708F8F7DE0728003401E6"
                    .HexToBytes();
            pubKey = Crypto.RecoverSignatureHashed(message, signature, true);
            Assert.AreEqual(
                "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower(),
                Crypto.ComputeAddress(pubKey).ToHex().ToLower()
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
            var rlp = tx.Rlp(false);
            // This is correct rlp with chain id 25. Check here: https://toolkit.abdk.consulting/ethereum#transaction
            var expectedRlp = 
                "0xee8085012a05f2008344aa20945f193b130d7c856179aa3d738ee06fab65e7314789056bc75e2d6310000080198080"
                    .HexToBytes();
            Assert.IsTrue(rlp.SequenceEqual(expectedRlp));

            rlp = tx.Rlp(true);
            // This is correct rlp with chain id 225. Check here: https://toolkit.abdk.consulting/ethereum#transaction
            expectedRlp = 
                "0xef8085012a05f2008344aa20945f193b130d7c856179aa3d738ee06fab65e7314789056bc75e2d631000008081e18080"
                    .HexToBytes();
            Assert.IsTrue(rlp.SequenceEqual(expectedRlp));

        }

        [Test]
        public void Test_External_Signature2()
        {
            var crypto = CryptoProvider.GetCrypto();

            var rawTx =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33";

            var ethTx = new LegacyTransactionChainId(rawTx.HexToBytes());
            Console.WriteLine("ETH RLP: " + ethTx.GetRLPEncodedRaw().ToHex());

            var nonce = ethTx.Nonce.ToHex();

            Console.WriteLine("Nonce " + nonce);
            Console.WriteLine("ChainId " + Convert.ToUInt64(ethTx.ChainId.ToHex(), 16));

            var tx = new Transaction
            {
                To = ethTx.ReceiveAddress.ToUInt160(),
                Value = ethTx.Value.ToUInt256(true),
                Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16)
            };

            Console.WriteLine("RLP: " + tx.Rlp(true).ToHex());

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

            var recoveredPubkey = crypto.RecoverSignatureHashed(message, signature, false);

            Console.WriteLine(recoveredPubkey.ToHex());

            var addr = crypto.ComputeAddress(recoveredPubkey);
            var same = addr.SequenceEqual(from.ToBytes());
            Console.WriteLine(addr.ToHex());
            Console.WriteLine(same);
        }
    }
}