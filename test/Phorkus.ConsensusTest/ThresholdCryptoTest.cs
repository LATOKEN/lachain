using System;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.ConsensusTest
{
    public class ThresholdCryptoTest
    {
        [Test]
        public void ThresholdKeyGen()
        {
            Mcl.Init();
            const int n = 7, f = 2;
            var keygen = new TrustedKeyGen(n, f, new Random(123321));
            var shares = keygen.GetPrivateShares().ToArray();
            var data = BitConverter.GetBytes(0xdeadbeef);
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            var signers = new IThresholdSigner[7];
            for (var i = 0; i < n; ++i)
            {
                signers[i] = new ThresholdSigner(data, shares[i], pubKeys);
                signers[i].SignatureProduced += (sender, signature) =>
                {
                    Assert.True(pubKeys.SharedPublicKey.ValidateSignature(signature, data));
                };
            }

            var signatureShares = signers.Select(signer => signer.Sign()).ToArray();
            for (var i = 0; i < n; ++i)
            for (var j = 0; j < n; ++j)
                signers[i].AddShare(pubKeys[j], signatureShares[j]);
        }
    }
}