using System.Collections.Generic;
using System.Linq;
using MCL.BLS12_381.Net;
using NUnit.Framework;

namespace Lachain.CryptoTest
{
    public class MclTests
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        [Repeat(100)]
        public void DeserializationG1Test()
        {
            var x = Fr.GetRandom();
            var a = G1.Generator * x;
            var enc = a.ToBytes();
            var b = G1.FromBytes(enc);
            Assert.True(a.Equals(b));
        }

        [Test]
        [Repeat(100)]
        public void DeserializationG2Test()
        {
            var x = Fr.GetRandom();
            var a = G2.Generator * x;
            var enc = a.ToBytes();
            var b = G2.FromBytes(enc);
            Assert.True(a.Equals(b));
        }

        [Test]
        [Repeat(100)]
        public void DeserializationFrTest()
        {
            var a = Fr.GetRandom();
            var enc = a.ToBytes();
            Assert.AreEqual(enc.Length, 32);
            var b = Fr.FromBytes(enc);
            Assert.True(a.Equals(b));
        }

        [Test]
        [Repeat(100)]
        public void AdditionG1Test()
        {
            var a = Fr.GetRandom();
            var b = Fr.GetRandom();

            var A = G1.Generator * a;
            var B = G1.Generator * b;
            var C = G1.Generator * (a + b);

            Assert.True(C.Equals(A + B), $"Addition of {a}G + {b}G failed");
        }

        [Test]
        [Repeat(100)]
        public void PairingTest()
        {
            var a = Fr.GetRandom();
            var b = Fr.GetRandom();

            var A = G1.Generator * a;
            var B = G2.Generator * b;

            var X = GT.Pairing(A, B);
            var Y = GT.Pow(GT.Pairing(G1.Generator, G2.Generator), a * b);
            Assert.True(X.Equals(Y));
        }

        private static Fr DummyEval(IEnumerable<Fr> poly, Fr x)
        {
            var res = Fr.Zero;
            var xPowI = Fr.One;
            foreach (var t in poly)
            {
                res += xPowI * t;
                xPowI *= x;
            }

            return res;
        }

        [Test]
        [Repeat(100)]
        public void EvalInterpolateTestFr()
        {
            const int n = 10;
            var poly = Enumerable.Range(0, n).Select(_ => Fr.GetRandom()).ToArray();
            var values = Enumerable.Range(100, n + 1)
                .Select(i => MclBls12381.EvaluatePolynomial(poly, Fr.FromInt(i)))
                .ToArray();
            for (var i = 0; i < n + 1; ++i)
                Assert.AreEqual(DummyEval(poly, Fr.FromInt(100 + i)), values[i]);
            var intercept = MclBls12381.LagrangeInterpolate(
                Enumerable.Range(100, n + 1).Select(Fr.FromInt).ToArray(),
                values
            );
            Assert.AreEqual(poly[0], intercept);
        }

        [Test]
        public void EvalFrPolyConstantTest()
        {
            var poly = new Fr[] {Fr.GetRandom()};
            var v0 = MclBls12381.EvaluatePolynomial(poly, Fr.Zero);
            var v1 = MclBls12381.EvaluatePolynomial(poly, Fr.One);
            var v2 = MclBls12381.EvaluatePolynomial(poly, Fr.FromInt(319948));
            Assert.AreEqual(poly[0], v0);
            Assert.AreEqual(poly[0], v1);
            Assert.AreEqual(poly[0], v2);
        }
    }
}