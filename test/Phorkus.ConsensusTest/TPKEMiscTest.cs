using System;
using System.Collections.Generic;
using NUnit.Framework;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.TPKE;

namespace Phorkus.ConsensusTest
{
    [TestFixture]
    public class TPKEMiscTest
    {
        private Random _rnd;

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            _rnd = new Random();
        }

        [Test]
        [Repeat(100)]
        public void CheckVerificationKeySerialization()
        {
            
            var Y = G1.Generator * Fr.GetRandom();
            var t = _rnd.Next();
            var n = _rnd.Next(0, 10);
            var Zs = new List<G2>();
            for (var i = 0; i < n; ++i)
            {
                Zs.Add(G2.Generator * Fr.GetRandom());
            }
            
            var vk = new VerificationKey(Y, t, Zs.ToArray());
            var enc = vk.ToProto();
            var vk2 = VerificationKey.FromProto(enc);
            
            Assert.True(vk.Equals(vk2));
        }

    }
}