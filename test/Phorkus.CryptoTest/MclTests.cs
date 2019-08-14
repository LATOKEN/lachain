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
            var rnd = new Random();
            var x = new Fr();
            x.SetInt(rnd.Next());
            var A = G1.Generator * x;
            
            byte[] enc = G1.ToBytes(A);
            var B = G1.FromBytes(enc);
            Assert.True(A.Equals(B));
        }

        [Test]
        [Repeat(100)]
        public void DeserializationFrTest()
        {
            var rnd = new Random();
            var a = Fr.FromInt(rnd.Next());
            byte[] enc = Fr.ToBytes(a);
            var b = Fr.FromBytes(enc);
            Assert.True(a.Equals(b));
        }

        [Test]
        [Repeat(100)]
        public void AdditionG1Test()
        {
            var rnd = new Random();

            // todo add really random Fr
            var a = Fr.FromInt(rnd.Next());
            var b = Fr.FromInt(rnd.Next());

            var A = G1.Generator * a;
            var B = G1.Generator * b;
            var C = G1.Generator * (a + b);
            
            Assert.True(C.Equals(A + B), $"Addition of {a}G + {b}G failed");
        }
    }
}