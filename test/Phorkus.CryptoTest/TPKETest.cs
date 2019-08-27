using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus.CommonCoin.ThresholdSignature;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.CryptoTest
{
    public class TPKETest 
    {
        const int N = 7, F = 2;
        public Random _rnd;
        private const int id = 132;
        
        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            _rnd = new Random();
        }
        
        [Test]
        [Repeat(100)]
        public void ThresholdKeyGen()
        {
            var keygen = new TPKETrustedKeyGen(N, F);

            var pubKey = keygen.GetPubKey();
            var verificationKey = keygen.GetVerificationKey();
            var privKeyTmp = new List<TPKEPrivKey>();
            for (var i = 0; i < N; ++i)
                privKeyTmp.Add(keygen.GetPrivKey(i));
            var privKey = privKeyTmp.ToArray();
            
            var data = new byte[32];
            _rnd.NextBytes(data);
            var share = new RawShare(data, id);

            var enc = pubKey.Encrypt(share);
            
            var chosen = new HashSet<int>();
            while (chosen.Count < F)
            {
                chosen.Add(_rnd.Next(0, N - 1));
            }

            var parts = new List<PartiallyDecryptedShare>();
            foreach (var i in chosen)
            {
                var dec = privKey[i].Decrypt(enc);
                Assert.True(verificationKey.Verify(enc, dec));
                parts.Add(dec);
            }

            var share2 = pubKey.FullDecrypt(enc, parts);

            Assert.AreEqual(share.Id, share2.Id);
            for (var i = 0; i < 32; ++i)
                Assert.AreEqual(share.Data[i], share2.Data[i]);
        }
    }
}