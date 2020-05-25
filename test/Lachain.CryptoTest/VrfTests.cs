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
            var privateKey = "D95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes();
            var seed = "74657374".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var (proof, value,j) = Vrf.Evaluate(privateKey, seed, role, 22, 999, 999);
        
            Assert.AreEqual(proof.ToHex(),"033b273afca4c841381c597225d2106c16f7f4a6edf4e6f51f4ad9ead880e1e994086eb8f46f7bf4e669a0b96d0d144be055f4005aa2d28bedf983f80e07dccbca71caef3a4ccfcd711953e8f2b156f6f7");
            Assert.True(value.ToHex() == "bef2460549094e40186b94eccdbc712636341601bae027aa81da90ba2a041e2c");
            Assert.True(j == 25);
        }
        
        [Test]
        [Repeat(1)]
        public void VerificationTest()
        {
            var publicKey = "02e5974f3e1e9599ff5af036b5d6057d80855e7182afb4c2fa1fe38bc6efb9072b".HexToBytes();
            var seed = "74657374".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var proof = "033b273afca4c841381c597225d2106c16f7f4a6edf4e6f51f4ad9ead880e1e994086eb8f46f7bf4e669a0b96d0d144be055f4005aa2d28bedf983f80e07dccbca71caef3a4ccfcd711953e8f2b156f6f7".HexToBytes();
            var success = Vrf.IsWinner(publicKey, proof, seed, role, 22, 999, 999);
            Assert.True(success);
        }
    }
}