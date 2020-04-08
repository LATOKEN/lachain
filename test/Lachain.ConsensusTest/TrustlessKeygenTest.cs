using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.Keygen;
using Lachain.Consensus.Keygen.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using NUnit.Framework;
using Lachain.Crypto.MCL.BLS12_381;
using Org.BouncyCastle.Crypto.Engines;

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
        public void RunAllHonest()
        {
            // const int n = 22, f = 7;
            const int n = 7, f = 2;
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

            var keys = keyGens.Select(x => x.TryGetKeys())
                .ToArray();
            Assert.IsFalse(keys.Any(x => x is null));

            for (var i = 0; i < n; ++i)
            {
                Assert.AreEqual(keys[0].TpkePublicKey, keys[i].TpkePublicKey);
                Assert.AreEqual(keys[0].ThresholdSignaturePublicKeySet, keys[i].ThresholdSignaturePublicKeySet);
            }
        }
    }
}