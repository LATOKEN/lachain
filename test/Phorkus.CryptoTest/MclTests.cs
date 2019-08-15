using System;
using NUnit.Framework;
using Org.BouncyCastle.Crypto.Digests;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Tests
{
    public class Tests
    {
        [SetUp]
        public void Setup()
        {
            Mcl.Init();
        }

        [Test]
        [Repeat(100)]
        public void DeserializationG1Test()
        {
            var x = Fr.GetRandom();
            var A = G1.Generator * x;
            
            byte[] enc = G1.ToBytes(A);
            var B = G1.FromBytes(enc);
            Assert.True(A.Equals(B));
        }

        [Test]
        [Repeat(100)]
        public void DeserializationFrTest()
        {
            var a = Fr.GetRandom();
            byte[] enc = Fr.ToBytes(a);
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

            var X = Mcl.Pairing(A, B);
            var Y = GT.Pow(Mcl.Pairing(G1.Generator, G2.Generator), (a * b));
            Assert.True(X.Equals(Y));
        }
    }
}