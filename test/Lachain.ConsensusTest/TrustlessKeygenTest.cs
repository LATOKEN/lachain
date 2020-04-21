using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using NUnit.Framework;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Utility.Utils;
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
            Mcl.Init();
        }

        private static Fr Eval(BiVarSymmetricPolynomial p, int x, int y)
        {
            var t = p.Evaluate(x).ToArray();
            return Mcl.GetValue(t, Fr.FromInt(y));
        }

        private static ThresholdKeyring[] SimulateKeygen(int n, int f)
        {
            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(_ => Crypto.GeneratePrivateKey())
                .Select(x => x.ToPrivateKey())
                .Select(x => new EcdsaKeyPair(x))
                .ToArray();

            var keyGens = Enumerable.Range(0, n)
                .Select(i => new TrustlessKeygen(ecdsaKeys[i], ecdsaKeys.Select(x => x.PublicKey), f))
                .ToArray();

            var messageLedger = new Queue<(int sender, object payload)>();
            messageLedger.Enqueue((-1, null));

            while (messageLedger.Count > 0)
            {
                var msg = messageLedger.Dequeue();
                switch (msg.payload)
                {
                    case null:
                        for (var i = 0; i < n; ++i)
                            messageLedger.Enqueue((i, keyGens[i].StartKeygen()));
                        break;
                    case CommitMessage commitMessage:
                        for (var i = 0; i < n; ++i)
                            messageLedger.Enqueue((i, keyGens[i].HandleCommit(msg.sender, commitMessage)));
                        break;
                    case ValueMessage valueMessage:
                        for (var i = 0; i < n; ++i)
                            keyGens[i].HandleSendValue(msg.sender, valueMessage);
                        break;
                    default:
                        Assert.Fail($"Message of type {msg.GetType()} occurred");
                        break;
                }
            }

            for (var i = 0; i < n; ++i) Assert.IsTrue(keyGens[i].Finished());

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
                .AssembleSignature(sigShares.Select((share, i) => new KeyValuePair<int, SignatureShare>(i, share)));
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
            var keys = SimulateKeygen(4, 1);
            CheckKeys(keys);
        }

        [Test]
        public void RunAllHonest_7_2()
        {
            var keys = SimulateKeygen(7, 2);
            CheckKeys(keys);
        }

        [Test]
        public void RunAllHonest_22_7()
        {
            var keys = SimulateKeygen(22, 7);
            CheckKeys(keys);
        }

        [Test]
        public void RunOneGuy()
        {
            var keys = SimulateKeygen(1, 0);
            CheckKeys(keys);
        }
    }
}