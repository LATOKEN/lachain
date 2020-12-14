using System;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class SerializationTest
    {
        [SetUp]
        public void SetUp()
        {
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
                "0xf1437606f337b000f00d69507434dbbd3b2abfa8a291d88551d92309fe3c222e0790fc847e849eb984bb807cba59170a0a63480e9457a375705ea2aba224a711e717ecddb224feeb738630945581bda24389f8fecf7865724361aec550e44919",
                G2.Generator.ToHex()
            );
            Assert.AreEqual(
                "0xd63f18cd560a715faef9b96c31e08eead86809ffd81642f6cdb55ed88ea2eca32cb2a28d818b7887cb35f1a7d4ab8a04eacc640851cb34b8a615729cd6d8cc4317125959b2012444b435508679e0a2f07d691d91aef2a18400a6696c6a3ae18d",
                (G2.Generator * Fr.FromInt(2)).ToHex()
            );
        }

        [Test]
        public void Test_Tpke_Serialization()
        {
            var publicKey = new PublicKey(G1.Generator, 134);
            Console.WriteLine(publicKey.ToHex());
            Console.WriteLine(PublicKey.FromBytes(publicKey.ToBytes()).ToHex());
            Assert.AreEqual(
                publicKey,
                PublicKey.FromBytes(publicKey.ToBytes())
            );
        }
    }
}