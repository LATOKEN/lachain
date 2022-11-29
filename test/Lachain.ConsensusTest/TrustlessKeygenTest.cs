using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using NUnit.Framework;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Utility.Containers;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class TrustlessKeygenTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        [SetUp]
        public void SetUp()
        {
        }

        private static Fr Eval(BiVarSymmetricPolynomial p, int x, int y)
        {
            var t = p.Evaluate(x).ToArray();
            return MclBls12381.EvaluatePolynomial(t, Fr.FromInt(y));
        }

        private class QueueItem
        {
            public int sender;
            public object? payload;

            public QueueItem(int sender, object? payload)
            {
                this.payload = payload;
                this.sender = sender;
            }
        }

        private static ThresholdKeyring[] SimulateKeygen(int n, int f, DeliveryServiceMode mode)
        {
            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(_ => Crypto.GeneratePrivateKey())
                .Select(x => x.ToPrivateKey())
                .Select(x => new EcdsaKeyPair(x))
                .ToArray();

            var keyGens = Enumerable.Range(0, n)
                .Select(i => new TrustlessKeygen(ecdsaKeys[i], ecdsaKeys.Select(x => x.PublicKey), f, 0))
                .ToArray();


            var messageLedger = new RandomSamplingQueue<QueueItem>();
            messageLedger.Enqueue(new QueueItem(-1, null));

            var curKeys = keyGens.Select(_ => (ThresholdKeyring?) null).ToArray();
            while (messageLedger.Count > 0)
            {
                QueueItem? msg;
                var success = mode switch
                {
                    DeliveryServiceMode.TAKE_FIRST => messageLedger.TryDequeue(out msg),
                    DeliveryServiceMode.TAKE_LAST => messageLedger.TryTakeLast(out msg),
                    DeliveryServiceMode.TAKE_RANDOM => messageLedger.TrySample(out msg),
                    _ => throw new NotImplementedException($"Unknown mode {mode}")
                };
                Assert.IsTrue(success);
                switch (msg?.payload)
                {
                    case null:
                        for (var i = 0; i < n; ++i)
                            messageLedger.Enqueue(new QueueItem(i, keyGens[i].StartKeygen()));
                        break;
                    case CommitMessage commitMessage:
                        for (var i = 0; i < n; ++i)
                            messageLedger.Enqueue(new QueueItem(i, keyGens[i].HandleCommit(msg.sender, commitMessage)));
                        break;
                    case ValueMessage valueMessage:
                        for (var i = 0; i < n; ++i)
                            keyGens[i].HandleSendValue(msg.sender, valueMessage);
                        break;
                    default:
                        Assert.Fail($"Message of type {msg.GetType()} occurred");
                        break;
                }

                for (var i = 0; i < n; ++i)
                {
                    var curKey = keyGens[i].TryGetKeys();
                    if (curKey is null)
                    {
                        Assert.AreEqual(null, curKeys[i]);
                        continue;
                    }

                    if (!curKeys[i].HasValue)
                    {
                        curKeys[i] = curKey;
                        continue;
                    }

                    Assert.IsTrue(curKey.Value.TpkePrivateKey.ToBytes()
                        .SequenceEqual(curKeys[i]!.Value.TpkePrivateKey.ToBytes()));
                    Assert.AreEqual(curKey.Value.PublicPartHash(), curKeys[i]!.Value.PublicPartHash());
                }

                for (var i = 0; i < n; ++i)
                {
                    keyGens[i] = TestSerializationRoundTrip(keyGens[i], ecdsaKeys[i]);
                }
            }

            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(keyGens[i].Finished());
                Assert.AreNotEqual(curKeys[i], null);
            }

            var keys = keyGens
                .Select(x => x.TryGetKeys() ?? throw new Exception())
                .ToArray();

            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(keys[0].TpkePublicKey, keys[i].TpkePublicKey);
                Assert.AreEqual(keys[0].ThresholdSignaturePublicKeySet, keys[i].ThresholdSignaturePublicKeySet);
            }

            return keys;
        }

        private static ThresholdKeyring[] SimulateKeygenFaultyBehavior(int n, int f, DeliveryServiceMode mode)
        {
            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(_ => Crypto.GeneratePrivateKey())
                .Select(x => x.ToPrivateKey())
                .Select(x => new EcdsaKeyPair(x))
                .ToArray();

            var maliciousKeygens = Enumerable.Range(0, f)
                .Select(i => new MaliciousKeygen(ecdsaKeys[i], ecdsaKeys.Select(x => x.PublicKey), f, 0))
                .ToArray();

            var validKeyGens = Enumerable.Range(f, n-f)
                .Select(i => new TrustlessKeygen(ecdsaKeys[i], ecdsaKeys.Select(x => x.PublicKey), f, 0))
                .ToArray();
            Assert.AreEqual(validKeyGens.Length, n-f);

            var messageLedger = new RandomSamplingQueue<QueueItem>();
            messageLedger.Enqueue(new QueueItem(-1, null));

            var curKeys = maliciousKeygens.Select(_ => (ThresholdKeyring?) null).ToArray();
            curKeys = curKeys.Concat(validKeyGens.Select(_ => (ThresholdKeyring?) null)).ToArray();
            while (messageLedger.Count > 0)
            {
                QueueItem? msg;
                var success = mode switch
                {
                    DeliveryServiceMode.TAKE_FIRST => messageLedger.TryDequeue(out msg),
                    DeliveryServiceMode.TAKE_LAST => messageLedger.TryTakeLast(out msg),
                    DeliveryServiceMode.TAKE_RANDOM => messageLedger.TrySample(out msg),
                    _ => throw new NotImplementedException($"Unknown mode {mode}")
                };
                
                Assert.IsTrue(success);
                try
                {
                    switch (msg?.payload)
                    {
                        case null:
                            for (var i = 0; i < f; ++i)
                                messageLedger.Enqueue(new QueueItem(i, maliciousKeygens[i].StartKeygen()));
                            for (var i = 0; i < n-f; ++i)
                                messageLedger.Enqueue(new QueueItem(i + f, validKeyGens[i].StartKeygen()));
                            break;
                        case CommitMessage commitMessage:
                            for (var i = 0; i < f; ++i)
                                messageLedger.Enqueue(new QueueItem(i, maliciousKeygens[i].HandleCommit(msg.sender, commitMessage)));
                            for (var i = 0; i < n-f; ++i)
                                messageLedger.Enqueue(new QueueItem(i + f, validKeyGens[i].HandleCommit(msg.sender, commitMessage)));
                            break;
                        case ValueMessage valueMessage:
                            for (var i = 0; i < f; ++i)
                                maliciousKeygens[i].HandleSendValue(msg.sender, valueMessage);
                            for (var i = 0; i < n-f; ++i)
                                validKeyGens[i].HandleSendValue(msg.sender, valueMessage);
                            break;
                        default:
                            Assert.Fail($"Message of type {msg.GetType()} occurred");
                            break;
                    }
                }
                catch (Exception)
                {
                }

                for (var i = 0; i < n; ++i)
                {
                    var curKey = (i < f) ? maliciousKeygens[i].TryGetKeys() : validKeyGens[i-f].TryGetKeys();
                    if (curKey is null)
                    {
                        Assert.AreEqual(null, curKeys[i]);
                        continue;
                    }

                    if (!curKeys[i].HasValue)
                    {
                        curKeys[i] = curKey;
                        continue;
                    }

                    Assert.IsTrue(curKey.Value.TpkePrivateKey.ToBytes()
                        .SequenceEqual(curKeys[i]!.Value.TpkePrivateKey.ToBytes()));
                    Assert.AreEqual(curKey.Value.PublicPartHash(), curKeys[i]!.Value.PublicPartHash());
                }

                for (var i = 0; i < f; ++i)
                {
                    maliciousKeygens[i] = TestSerializationRoundTripMalicious(maliciousKeygens[i], ecdsaKeys[i]);
                }
                for (var i = 0; i < n-f; i++)
                {
                    validKeyGens[i] = TestSerializationRoundTrip(validKeyGens[i], ecdsaKeys[i+f]);
                }
            }

            for (var i = 0 ; i < f; i++)
            {
                Assert.IsTrue(maliciousKeygens[i].Finished());
            }
            for (var i = 0 ; i < n-f; i++)
            {
                Assert.IsTrue(validKeyGens[i].Finished());
            }

            for (var i = 0; i < n; ++i)
            {
                Assert.AreNotEqual(curKeys[i], null);
            }

            var keys = maliciousKeygens
                .Select(x => x.TryGetKeys() ?? throw new Exception())
                .ToArray();
            keys = keys.Concat(validKeyGens
                .Select(x => x.TryGetKeys() ?? throw new Exception())).ToArray();

            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(keys[0].TpkePublicKey, keys[i].TpkePublicKey);
                Assert.AreEqual(keys[0].ThresholdSignaturePublicKeySet, keys[i].ThresholdSignaturePublicKeySet);
            }

            return keys;
        }

        private static MaliciousKeygen TestSerializationRoundTripMalicious(MaliciousKeygen keyGen, EcdsaKeyPair keyPair)
        {
            var bytes = keyGen.ToBytes();
            var restored = MaliciousKeygen.FromBytes(bytes, keyPair);
            Assert.AreEqual(restored, keyGen);
            return restored;
        }

        private static TrustlessKeygen TestSerializationRoundTrip(TrustlessKeygen keyGen, EcdsaKeyPair keyPair)
        {
            var bytes = keyGen.ToBytes();
            var restored = TrustlessKeygen.FromBytes(bytes, keyPair);
            Assert.AreEqual(restored, keyGen);
            return restored;
        }

        private void CheckKeys(IList<ThresholdKeyring> keys)
        {
            var payload = "0xDeadBeef".HexToBytes();
            var ciphertext = keys[0].TpkePublicKey.Encrypt(new RawShare(payload, 0));
            var partiallyDecryptedShares = keys
                .Select(keyring => keyring.TpkePrivateKey)
                .Select(key => key.Decrypt(ciphertext))
                .ToList();

            var restored = keys[0].TpkePublicKey.FullDecrypt(ciphertext, partiallyDecryptedShares);
            Assert.AreEqual(payload.ToHex(), restored.Data.ToHex());

            var sigShares = keys
                .Select(keyring => keyring.ThresholdSignaturePrivateKey)
                .Select(key => key.HashAndSign(payload))
                .ToArray();
            foreach (var (share, i) in sigShares.WithIndex())
            foreach (var keyring in keys)
                Assert.IsTrue(keyring.ThresholdSignaturePublicKeySet[i].ValidateSignature(share, payload));

            var sig = keys[0].ThresholdSignaturePublicKeySet
                .AssembleSignature(sigShares.Select((share, i) => new KeyValuePair<int, Signature>(i, share)));
            foreach (var keyring in keys)
                Assert.IsTrue(keyring.ThresholdSignaturePublicKeySet.SharedPublicKey.ValidateSignature(sig, payload));
        }

        [Test]
        [Repeat(100)]
        public void SymmetricPolynomialIsSymmetric()
        {
            var rnd = new Random();
            var p = BiVarSymmetricPolynomial.Random(5);
            int x = rnd.Next(), y = rnd.Next();
            var vxy = Eval(p, x, y);
            var vyx = Eval(p, y, x);
            Assert.AreEqual(vxy, vyx);

            var c = p.Commit();
            var cxy = c.Evaluate(x, y);
            var cyx = c.Evaluate(y, x);
            Assert.AreEqual(cxy, cyx);
        }

        [Test]
        public void RunAllHonest_4_1()
        {
            var keys = SimulateKeygen(4, 1, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_4_1_RandomOrder()
        {
            var keys = SimulateKeygen(4, 1, DeliveryServiceMode.TAKE_RANDOM);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_4_1_ReverseOrder()
        {
            var keys = SimulateKeygen(4, 1, DeliveryServiceMode.TAKE_LAST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_7_0()
        {
            var keys = SimulateKeygen(7, 0, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_7_2()
        {
            var keys = SimulateKeygen(7, 2, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }

        [Test]
        public void RunAllHonest_7_2_RandomOrder()
        {
            var keys = SimulateKeygen(7, 2, DeliveryServiceMode.TAKE_RANDOM);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_7_2_ReverseOrder()
        {
            var keys = SimulateKeygen(7, 2, DeliveryServiceMode.TAKE_LAST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunOneGuy()
        {
            var keys = SimulateKeygen(1, 0, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunOneGuy_RandomOrder()
        {
            var keys = SimulateKeygen(1, 0, DeliveryServiceMode.TAKE_RANDOM);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunOneGuy_ReverseOrder()
        {
            var keys = SimulateKeygen(1, 0, DeliveryServiceMode.TAKE_LAST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_2_0()
        {
            var keys = SimulateKeygen(2, 0, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }
        
        [Test]
        public void RunAllHonest_3_0()
        {
            var keys = SimulateKeygen(3, 0, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }

        [Test]
        public void RunSomeMalicious_7_2()
        {
            var keys = SimulateKeygenFaultyBehavior(7, 2, DeliveryServiceMode.TAKE_FIRST);
            CheckKeys(keys);
        }
    }
}