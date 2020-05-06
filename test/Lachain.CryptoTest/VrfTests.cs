using System;
using System.Security.Cryptography;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.VRF;
using Lachain.Utility.Utils;
using Nethereum.Hex.HexConvertors.Extensions;
using NUnit.Framework;

namespace Lachain.CryptoTest
{
    public class VrfTests
    {
        [Test]
        [Repeat(1)]
        public void EvaluationTest()
        {
            var privateKey = "85db638bfc1b7215461db393af4611cab3a0834535a26dcf2a129ec965007d88".HexToBytes();
            var seed = "746573743639343237373434".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var (proof, value,j) = Vrf.Evaluate(privateKey, seed, role, 22, 50, 100);

            Assert.True(proof.ToHex() == "033efd8d2b86f4a36fc35307a4c4983c5fca266c428cdf423914f8864749421215884bcd3cd371a02312a01c2f14e814bc7382904ed5631fc5b69e951e433d76777a79151d1b8ab4483b267d1b662ee474");
            Assert.True(value.ToHex() == "3d26050c1b7d3ea27f83f6b629a135a5989384cfca7ff8f914751038a5efb64c");
            Assert.True(j == 9);
        }
        
        [Test]
        [Repeat(1)]
        public void VerificationTest()
        {
            var publicKey = "047975d23cd1bacf91232ab1539fa7c7ff4c9d411587f9e20173a2bf7d617ce06a691ccc3874ccd88acdf72154d0baee9ad37ea0c0b120322f3b95a2d58bea8c4d".HexToBytes();
            var seed = "746573743639343237373434".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var proof = "033efd8d2b86f4a36fc35307a4c4983c5fca266c428cdf423914f8864749421215884bcd3cd371a02312a01c2f14e814bc7382904ed5631fc5b69e951e433d76777a79151d1b8ab4483b267d1b662ee474".HexToBytes();
            var success = Vrf.IsWinner(publicKey, proof, seed, role, 22, 50, 100);
            Assert.True(success);
        }
    }
}