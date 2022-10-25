using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.CryptoTest
{
    //[Ignore("tpke is not used for now")]
    public class ThresholdEncryptionTest
    {
        private const int N = 7, F = 2;
        private const int Id = 132;
        private Random _rnd;

        [SetUp]
        public void SetUp()
        {
            _rnd = new Random((int) TimeUtils.CurrentTimeMillis());
        }

        [Test]
        public void Test_ThresholdEncryption()
        {
            var keygen = new TrustedKeyGen(N, F);
            // var verificationKey = keygen.GetVerificationKey();
            var privKeys = keygen.GetPrivateShares().ToArray();
            var pubKeySet = new PublicKeySet(privKeys.Select(x => x.GetPublicKeyShare()), F);
            var shares = new IRawShare[N];
            var encrypedShares = new EncryptedShare[N];
            var thresholdEncryptor = new IThresholdEncryptor[N];
            var decryptedShares = new List<PartiallyDecryptedShare>[N];
            for (int i = 0; i < N ; i++)
            {
                var len = _rnd.Next() % 10 + 1;
                var data = new byte[len];
                _rnd.NextBytes(data);
                shares[i] = new RawShare(data, i);
                thresholdEncryptor[i] = new ThresholdEncryptor(privKeys[i], pubKeySet, false);
                encrypedShares[i] = thresholdEncryptor[i].Encrypt(shares[i]);
            }

            for (int i = 0 ; i < N ; i++)
            {
                decryptedShares[i] = thresholdEncryptor[i].AddEncryptedShares(encrypedShares.ToList());
            }

            for (int i = 0 ; i < N ; i++)
            {
                for (int sender = 0 ; sender < N ; sender++)
                {
                    foreach (var dec in decryptedShares[sender])
                    {
                        var msg = CreateDecryptedMessage(dec);
                        Assert.IsTrue(thresholdEncryptor[i].AddDecryptedShare(msg.Decrypted, sender));
                    }
                }

                for (int shareId = 0; shareId < N ; shareId++)
                {
                    Assert.IsTrue(thresholdEncryptor[i].CheckDecryptedShares(shareId));
                    if (shareId < N-1) Assert.IsFalse(thresholdEncryptor[i].GetResult(out var result));
                    else Assert.IsTrue(thresholdEncryptor[i].GetResult(out var result));
                }

                Assert.IsTrue(thresholdEncryptor[i].GetResult(out var finalResult));
                var rawShares = finalResult!.ToArray();
                for (int j = 0 ; j < N ; j++)
                {
                    Assert.AreEqual(rawShares[j], shares[j]);
                }
            }
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

        private ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = share.Encode()
            };
            return message;
        }
    }
}