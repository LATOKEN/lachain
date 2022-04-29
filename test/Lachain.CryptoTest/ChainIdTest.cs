using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
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
using Lachain.Core.DI;
using Lachain.Core.Config;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.CLI;
using Lachain.UtilityTest;
using Lachain.Networking;
using Lachain.Utility.Benchmark;

namespace Lachain.CryptoTest
{
    public class ChainIdTest
    {
        private static readonly ILogger<ChainIdTest> Logger = LoggerFactory.GetLoggerForClass<ChainIdTest>();
        private static readonly TimeBenchmark EcSign = new TimeBenchmark();
        private static readonly TimeBenchmark EcVerify = new TimeBenchmark();
        private static readonly TimeBenchmark EcRecover = new TimeBenchmark();
        private static int _oldChainId;
        private static int _newChainId;
        // set these values properly for the tests to work
        private static int _lowestValidChainId = 25; // don't know the lowest value but must be positive
        private static int _highestValidChainId = 109;
        private static readonly Secp256k1 Secp256K1 = new Secp256k1();
        private IContainer? _container;
        private IConfigManager _configManager = null!;

        [SetUp]
        public void Setup()
        {
            _container?.Dispose() ;
            TestUtils.DeleteTestChainData();
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));
            
            containerBuilder.RegisterModule<ConfigModule>();
            _container = containerBuilder.Build();
            _configManager = _container.Resolve<IConfigManager>();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        [Repeat(2)]
        public void Test_SignatureWithMultipleNetwork()
        {
            TestForNetwork();
            TestForNetwork("local");
            TestForNetwork("devnet");
            TestForNetwork("testnet");
            TestForNetwork("mainnet");
        }


        [Test]
        [Repeat(2)]
        public void Test_SignatureWithMultipleChainId()
        {
            var chainIds = new List<(int,int)>();
            // Add some chain id to test
            int total = 10;
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            for (int i = 0 ; i < total ; i++)
            {
                chainIds.Add(GetRandomChainId(_lowestValidChainId, _highestValidChainId, rng));
            }

            foreach (var (oldChainId, newChainId) in chainIds)
            {
                Logger.LogInformation($"old chain id: {oldChainId}, new chain id: {newChainId}");
                SetChainId(oldChainId, newChainId);
                Assert.AreEqual(oldChainId, ChainId(false));
                Assert.AreEqual(newChainId, ChainId(true));
                int noOfTest = 10;
                for (var it = 0 ; it < noOfTest ; it++)
                {
                    var key = GetRandomKey();
                    VerifyPublicKey(key.PublicKey);
                    SignMessageAndVerify(key);
                }
            }
        }

        public EcdsaKeyPair GetRandomKey()
        {
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] random = new byte[32];
            rng.GetBytes(random);
            return new EcdsaKeyPair(random.ToPrivateKey());
        }

        public void TestForNetwork(string? networkName = null)
        {
            BuildForNetwork(networkName);
            var network = _configManager.GetConfig<NetworkConfig>("network") ??
                throw new ApplicationException("No network section in config");
            Assert.That(network.NewChainId != null);
            var oldChainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
            var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
            SetChainId(oldChainId!.Value, newChainId!.Value);

            Logger.LogInformation($"old chain id: {ChainId(false)}, new chain id: {ChainId(true)}");
            Assert.AreEqual(ChainId(false), oldChainId);
            Assert.AreEqual(ChainId(true), newChainId);

            int noOfTest = 10;
            for (var it = 0 ; it < noOfTest ; it++)
            {
                var key = GetRandomKey();
                VerifyPublicKey(key.PublicKey);
                SignMessageAndVerify(key);
            }
        }

        public void BuildForNetwork(string? networkName = null)
        {
            Teardown();
            string configPath = "config";
            if (networkName != null)
            {
                configPath += "_";
                configPath += networkName;
            }
            configPath += ".json";
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), configPath),
                new RunOptions()
            ));
            
            containerBuilder.RegisterModule<ConfigModule>();
            _container = containerBuilder.Build();
            _configManager = _container.Resolve<IConfigManager>();
        }

        public void SignMessageAndVerify(EcdsaKeyPair key)
        {
            byte[] random = new byte[32];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            var publicKey = key.PublicKey.Buffer.ToByteArray();
            int n = 100;
            for (var it = 0; it < n; ++it)
            {
                rng.GetBytes(random);
                var message = random.ToHex()
                    + "ec808504a817c800825208948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080"
                    + it.ToHex().Substring(2);
                rng.GetBytes(random);
                message += random.ToHex().Substring(2);
                var digest = message.HexToBytes();
                // using new chain id
                var signature = Sign(digest, key.PrivateKey.Buffer.ToByteArray(), true);
                Assert.IsTrue(VerifySignature(digest, signature, publicKey, true));
                var recoveredPubkey = RecoverSignature(digest, signature, true);
                Assert.AreEqual(recoveredPubkey, publicKey);
                // using old chain id
                signature = Sign(digest, key.PrivateKey.Buffer.ToByteArray(), false);
                Assert.IsTrue(VerifySignature(digest, signature, publicKey, false));
                recoveredPubkey = RecoverSignature(digest, signature, false);
                Assert.AreEqual(recoveredPubkey, publicKey);
            }
        }

        // mimic of SignatureSize of DefaultCrypto.cs but using different chain id
        public int SignatureSize(bool useNewChainId)
        {
            return useNewChainId ? 66 : 65;
        }

        // mimic of RestoreEncodedRecIdFromSignatureBuffer of DefaultCrypto.cs but using different chain id
        public int RestoreEncodedRecIdFromSignatureBuffer(byte[] signature)
        {
            var recIdBytes = new byte[4];
            recIdBytes[0] = signature[64];
            if (signature.Length > 65)
                recIdBytes[1] = signature[65];
            return BitConverter.ToInt32(recIdBytes);
        }

        // mimic of VerifySignature of DefaultCrypto.cs but using different chain id
        public bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey, bool useNewChainId)
        {
            var messageHash = message.KeccakBytes();
            return VerifySignatureHashed(messageHash, signature, publicKey, useNewChainId);
        }

        // mimic of VerifySignatureHashed of DefaultCrypto.cs but using different chain id
        public bool VerifySignatureHashed(byte[] messageHash, byte[] signature, byte[] publicKey, bool useNewChainId)
        {
            if (messageHash.Length != 32 || signature.Length != SignatureSize(useNewChainId)) return false;
            return EcVerify.Benchmark(() =>
            {
                var pk = new byte[64];
                if (!Secp256K1.PublicKeyParse(pk, publicKey))
                    return false;

                var publicKeySerialized = new byte[33];
                if (!Secp256K1.PublicKeySerialize(publicKeySerialized, pk, Flags.SECP256K1_EC_COMPRESSED))
                    throw new Exception("Cannot serialize parsed key: how did it happen?");

                var parsedSig = new byte[65];
                var recId = (RestoreEncodedRecIdFromSignatureBuffer(signature) - 36) / 2 / ChainId (useNewChainId);
                if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Take(64).ToArray(), recId))
                    return false;

                return Secp256K1.Verify(parsedSig.Take(64).ToArray(), messageHash, pk);
            });
        }

        // mimic of Sign of DefaultCrypto.cs but using different chain id
        public byte[] Sign(byte[] message, byte[] privateKey, bool useNewChainId)
        {
            var messageHash = message.KeccakBytes();
            return SignHashed(messageHash, privateKey, useNewChainId);
        }

        // mimic of SignHashed of DefaultCrypto.cs but using different chain id
        public byte[] SignHashed(byte[] messageHash, byte[] privateKey, bool useNewChainId)
        {
            if (privateKey.Length != 32) throw new ArgumentException(nameof(privateKey));
            if (messageHash.Length != 32) throw new ArgumentException(nameof(messageHash));
            return EcSign.Benchmark(() =>
            {
                var sig = new byte[65];
                if (!Secp256K1.SignRecoverable(sig, messageHash, privateKey))
                    throw new Exception("secp256k1.sign_recoverable failed");
                var serialized = new byte[64];
                if (!Secp256K1.RecoverableSignatureSerializeCompact(serialized, out var recId, sig))
                    throw new Exception("Cannot serialize recoverable signature: how did it happen?");
                recId = ChainId(useNewChainId) * 2 + 35 + recId;
                var recIdBytes = new byte[useNewChainId ? 2 : 1];
                var fullBin = recId.ToBytes().ToArray();
                recIdBytes[0] = fullBin[0];
                if (useNewChainId)
                {
                    recIdBytes[1] = fullBin[1];
                }
                return serialized.Concat(recIdBytes).ToArray();
            });
        }

        // mimic of RecoverSignature of DefaultCrypto.cs but using different chain id
        public byte[] RecoverSignature(byte[] message, byte[] signature, bool useNewChainId)
        {
            var messageHash = message.KeccakBytes();
            return RecoverSignatureHashed(messageHash, signature, useNewChainId);
        }

        // mimic of RecoverSignatureHashed of DefaultCrypto.cs but using different chain id
        public byte[] RecoverSignatureHashed(byte[] messageHash, byte[] signature, bool useNewChainId)
        {
            if (messageHash.Length != 32) throw new ArgumentException(nameof(messageHash));
            if (signature.Length != SignatureSize(useNewChainId)) throw new ArgumentException(nameof(signature));
            return EcRecover.Benchmark(() =>
            {
                var parsedSig = new byte[65];
                var pk = new byte[64];
                var encodedRecId = RestoreEncodedRecIdFromSignatureBuffer(signature);
                var recId = (encodedRecId - 36) / 2 / ChainId(useNewChainId);
                if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Take(64).ToArray(), recId))
                    throw new ArgumentException(nameof(signature));
                if (!Secp256K1.Recover(pk, parsedSig, messageHash))
                    throw new ArgumentException("Bad signature");
                var result = new byte[33];
                if (!Secp256K1.PublicKeySerialize(result, pk, Flags.SECP256K1_EC_COMPRESSED))
                    throw new Exception("Cannot serialize recovered public key: how did it happen?");
                return result;
            });
        }

        public void VerifyPublicKey(ECDSAPublicKey pubkey)
        {
            var pk = new byte[64];
            var publicKey = pubkey.Buffer.ToByteArray();
            Assert.IsTrue(Secp256K1.PublicKeyParse(pk, publicKey));
            var publicKeySerialized = new byte[33];
            Assert.IsTrue(Secp256K1.PublicKeySerialize(publicKeySerialized, pk, Flags.SECP256K1_EC_COMPRESSED));
            Assert.AreEqual(publicKey, publicKeySerialized);
        }

        public (int,int) GetRandomChainId(int lowestChainId, int highestChainId, RNGCryptoServiceProvider rng)
        {
            var range = highestChainId - lowestChainId + 1;
            return (GetRandomByteInRange(range, rng) + lowestChainId, GetRandomByteInRange(range, rng) + lowestChainId);
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
            _oldChainId = oldChainId;
            _newChainId = newChainId;
        }

        private int ChainId(bool useNewChainId)
        {
            if (useNewChainId) return _newChainId;
            return _oldChainId;
        }

    }
}