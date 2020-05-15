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
            var privateKey = "391091cb26f6a1c0b7108bc68d06a5c7bf32d06b5910976e6a07b4dcb54f4fcc".HexToBytes();
            var seed = "746573743639343237373434".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var (proof, value,j) = Vrf.evaluate(privateKey, seed, role, 22, 50, 100);

            Assert.True(proof.ToHex() == "029347c71ec5a6ef8f4d11411316827cd8a91b0b0770ee2fd9e9f71f40ed34eb36e40ae21cf77d0eecb19198f7f78382c267b3a329884b9f86e8df6c48d122fab44d33e746c0480be5cc484d3cd1c5e1aa");
            Assert.True(value.ToHex() == "95ff220621f360f983eb342927a0b33baf1d10751387094e657fdfc6ce0efc4c");
            Assert.True(j == 12);
        }
        
        [Test]
        [Repeat(1)]
        public void VerificationTest()
        {
            var publicKey = "0405d73fd2774e66668841848dd2bafacb4acceacbdd1b36be237bc1f766911342a9bca2d1e3945801d10d489a97a17a80dcd1148709176ad317597fe5d16800ea".HexToBytes();
            var seed = "746573743639343237373434".HexToBytes();
            var role = "7374616b6572".HexToBytes();
            var proof = "029347c71ec5a6ef8f4d11411316827cd8a91b0b0770ee2fd9e9f71f40ed34eb36e40ae21cf77d0eecb19198f7f78382c267b3a329884b9f86e8df6c48d122fab44d33e746c0480be5cc484d3cd1c5e1aa".HexToBytes();
            var success = Vrf.isWinner(publicKey, proof, seed, role, 22, 50, 100);
            Assert.True(success);
        }
    }
}