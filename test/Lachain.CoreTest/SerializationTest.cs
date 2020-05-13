using System;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Utility.Serialization;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class SerializationTest
    {
        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
        }

        [Test]
        public void Test_Mcl_Serializations()
        {
            Assert.AreEqual(
                "0x0000000000000000000000000000000000000000000000000000000000000000",
                Fr.FromInt(0).ToHex()
            );
            Assert.AreEqual(
                "0x0100000000000000000000000000000000000000000000000000000000000000",
                Fr.FromInt(1).ToHex()
            );
            var fr = Fr.GetRandom();
            Assert.AreEqual(fr, Fr.FromBytes(fr.ToBytes()));

            Assert.AreEqual(
                "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                G1.Zero.ToHex()
            );
            Assert.AreEqual(
                "0xe9328f8eb8185341f22adaf2bf41f66258d97b2b5b2dbd2c27a77c81d9b5d76dd119bf7b1cd5d57b1273f9c4a654540e",
                G1.Generator.ToHex()
            );
            Assert.AreEqual(
                "0x2ac325b200d53184871fb8f8f5e5e43b302349ae6172de2899e88d2961f0bd2593b427667f5f85b4b59296ae8dcfc918",
                (G1.Generator * Fr.FromInt(2)).ToHex()
            );
            var g1 = G1.Generator * Fr.GetRandom();
            Assert.AreEqual(g1, G1.FromBytes(g1.ToBytes()));

            Assert.AreEqual(
                "0x000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000",
                G2.Zero.ToHex()
            );
            Assert.AreEqual(
                "0x770d1d7e912474168480b3ac0b0c80bca01cdf9ce7ac1a301945074a8ccf4f1934e5f32eea6ae2c97198fc7555722903c06e4ce90382080c13e2a8c78dd07e4e2b6d00dff70f899c6c3e3752e64a563e03fbe67b93589e6eb0a140e7d8706398",
                G2.Generator.ToHex()
            );
            Assert.AreEqual(
                "0x9acfd419fbb6d59189d7cff91b0a76749572f468f4f4941084e09868fb35ded7a73a6f3cb0cefa12c85e9a31da8f460aca5d3947ff02817cf0af281b563845c2d3cae1442dee819dea1a048b6253b338930792237d2d10b225e07a79abfce38f",
                (G2.Generator * Fr.FromInt(2)).ToHex()
            );
        }
    }
}