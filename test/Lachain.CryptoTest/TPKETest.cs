using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;

namespace Lachain.CryptoTest
{
    public class TpkeTest
    {
        private const int N = 7, F = 2;
        private const int Id = 132;
        private Random _rnd;

        [SetUp]
        public void SetUp()
        {
            _rnd = new Random();
        }

        [Test]
        [Repeat(100)]
        public void ThresholdKeyGen()
        {
            var keygen = new TrustedKeyGen(N, F);

            var pubKey = keygen.GetPubKey();
            // var verificationKey = keygen.GetVerificationKey();
            var privKeyTmp = new List<PrivateKey>();
            for (var i = 0; i < N; ++i)
                privKeyTmp.Add(keygen.GetPrivKey(i));
            var privKey = privKeyTmp.ToArray();

            var len = _rnd.Next() % 100 + 1;
            var data = new byte[len];
            _rnd.NextBytes(data);
            var share = new RawShare(data, Id);

            var enc = pubKey.Encrypt(share);

            var chosen = new HashSet<int>();
            while (chosen.Count < F) chosen.Add(_rnd.Next(0, N - 1));

            var parts = new List<PartiallyDecryptedShare>();
            foreach (var dec in chosen.Select(i => privKey[i].Decrypt(enc)))
            {
                // Assert.True(verificationKey.Verify(enc, dec));
                parts.Add(dec);
            }

            var share2 = pubKey.FullDecrypt(enc, parts);

            Assert.AreEqual(share.Id, share2.Id);
            for (var i = 0; i < len; ++i)
                Assert.AreEqual(share.Data[i], share2.Data[i]);
        }

        // [Test]
        // [Repeat(100)]
        // public void CheckVerificationKeySerialization()
        // {
        //     var y = G1.Generator * Fr.GetRandom();
        //     var t = _rnd.Next();
        //     var n = _rnd.Next(0, 10);
        //     var zs = new List<G2>();
        //     for (var i = 0; i < n; ++i) zs.Add(G2.Generator * Fr.GetRandom());
        //
        //     var vk = new VerificationKey(y, t, zs.ToArray());
        //     var enc = vk.ToProto();
        //     var vk2 = VerificationKey.FromProto(enc);
        //
        //     Assert.True(vk.Equals(vk2));
        // }
    }
}