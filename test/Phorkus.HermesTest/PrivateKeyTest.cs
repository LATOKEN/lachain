using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phorkus.Hermes;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Signer;
using Phorkus.Proto;

namespace Phorkus.HermesTest
{
    [TestClass]
    public class PrivateKeyTest
    {
        private PublicKey ToPublicKey(byte[] bytes)
        {
            return new PublicKey
            {
                Buffer = ByteString.CopyFrom(bytes)
            };
        }
        
        [TestMethod]
        public void Test()
        {
            var participants = new Dictionary<PublicKey, int>
            {
                { ToPublicKey(HexUtil.hexToBytes("02affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5")), 1 },
                { ToPublicKey(HexUtil.hexToBytes("0252b662232efa6affe522a78fbe06df7bb5809db64a165cffa1dbb3154722389a")), 2 },
                { ToPublicKey(HexUtil.hexToBytes("038871c219368549f7765f94c0b7b3046612f08e626771e98e235f4abb7ae363b9")), 3 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7364")), 4 }
            };

            var myKey = ToPublicKey(
                HexUtil.hexToBytes("0252b662232efa6affe522a78fbe06df7bb5809db64a165cffa1dbb3154722389a"));

            var protos = new IGeneratorProtocol[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                protos[i] = new DefaultGeneratorProtocol(participants, myKey);
            
            Console.WriteLine("Initializing protocol");
            
            for (var i = 0; i < participants.Count; i++)
                protos[i].Initialize();

            Console.WriteLine("Generating shares");
            
            var shares = new IReadOnlyCollection<BgwPublicParams>[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                shares[i] = protos[i].GenerateShare();

            var flippedShares = new BgwPublicParams[participants.Count][];
            for (var i = 0; i < participants.Count; i++)
                flippedShares[i] = new BgwPublicParams[participants.Count];
            
            for (var i = 0; i < participants.Count; i++)
            {
                var si = shares[i].ToArray();
                for (var j = 0; j < si.Length; j++)
                {
                    flippedShares[j][i] = si[j];
                }
            }
            
            Console.WriteLine("Generating point");
            
            var points = new BGWNPoint[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                points[i] = protos[i].GeneratePoint(flippedShares[i]);

            BiprimalityTestResult biprimalityTestResult = null;
            for (var round = 0; round < 10; round++)
            {
                Console.WriteLine($"Testing biprimality {round}/10");
                
                var proofs = new QiTestForRound[participants.Count];
                for (var i = 0; i < participants.Count; i++)
                    proofs[i] = protos[i].GenerateProof(points);
                
                for (var i = 0; i < participants.Count; i++)
                {
                    try
                    {
                        var test = protos[i].ValidateProof(proofs);
                        if (!test.passes)
                            continue;
                        biprimalityTestResult = test;
                    }
                    catch (Exception)
                    {
                        // ignore
                        break;
                    }
                }
                if (biprimalityTestResult != null)
                    break;
            }
            if (biprimalityTestResult is null)
                throw new Exception("Unable to find valid biprimality test");

            Console.WriteLine("Generating derivations");
            
            var derivations = new IReadOnlyCollection<KeysDerivationPublicParameters>[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                derivations[i] = protos[i].GenerateDerivation(biprimalityTestResult);
            var flippedDerivation = new KeysDerivationPublicParameters[participants.Count][];
            for (var i = 0; i < participants.Count; i++)
                flippedDerivation[i] = new KeysDerivationPublicParameters[participants.Count];
            
            for (var i = 0; i < participants.Count; i++)
            {
                var si = derivations[i].ToArray();
                for (var j = 0; j < si.Length; j++)
                {
                    flippedDerivation[j][i] = si[j];
                }
            }

            Console.WriteLine("Generating thetas");
            
            var thetas = new ThetaPoint[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                thetas[i] = protos[i].GenerateTheta(flippedDerivation[i]);

            Console.WriteLine("Generating verification codes");
            
            var verificationKeys = new VerificationKey[participants.Count];
            for (var i = 0; i < participants.Count; i++)
                verificationKeys[i] = protos[i].GenerateVerification(thetas);

            Console.WriteLine("Finalizing");
            
            for (var i = 0; i < participants.Count; i++)
            {
                var PrivateKey = protos[i].Finalize(verificationKeys);
                Console.WriteLine("Private Key: " + HexUtil.bytesToHex(PrivateKey.toByteArray()));
            }
        }
    }
}