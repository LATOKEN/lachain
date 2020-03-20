using System;
using System.Linq;
using NUnit.Framework;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Utility.Utils;

namespace Lachain.CryptoTest
{
    public class ThresholdCryptoTest
    {
        [Test]
        public void ThresholdKeyGen()
        {
            Mcl.Init();
            const int n = 7, f = 2;
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var data = BitConverter.GetBytes(0xdeadbeef);
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            var signers = new IThresholdSigner[n];
            for (var i = 0; i < n; ++i)
            {
                signers[i] = new ThresholdSigner(data, shares[i], pubKeys);
            }

            var signatureShares = signers.Select(signer => signer.Sign()).ToArray();
            var sigs = new Signature[n];
            var success = new bool[n];
            for (var i = 0; i < n; ++i) success[i] = true;
            for (var i = 0; i < n; ++i)
            {
                for (var j = 0; j < n; ++j)
                {
                    success[i] &= signers[i].AddShare(pubKeys[j], signatureShares[j], out var sig);
                    if (sigs[i] == null && sig != null)
                        sigs[i] = sig;
                }
            }

            for (int i = 0; i < n; ++i)
            {
                Assert.IsTrue(success[i], $"Player {i} did not terminate successfully");
                Assert.IsTrue(pubKeys.SharedPublicKey.ValidateSignature(sigs[i], data));
            }
        }
    }
}