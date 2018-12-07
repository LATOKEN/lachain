using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Hermes;
using Phorkus.Hermes.Generator;
using Phorkus.Hermes.Generator.Messages;
using Phorkus.Hermes.Generator.State;
using Phorkus.Hermes.Signer;
using Phorkus.Proto;
using Phorkus.Utility;
using Phorkus.Utility.Utils;

namespace HermesTestConsole
{
    class Program
    {
        private static PublicKey ToPublicKey(byte[] bytes)
        {
            return new PublicKey
            {
                Buffer = ByteString.CopyFrom(bytes)
            };
        }
        
        static void Main(string[] args)
        {
            var participants = new SortedDictionary<PublicKey, int>(new PublicKeyComparer())
            {
                { ToPublicKey(HexUtil.hexToBytes("02affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5")), 1 },
                { ToPublicKey(HexUtil.hexToBytes("0252b662232efa6affe522a78fbe06df7bb5809db64a165cffa1dbb3154722389a")), 2 },
                { ToPublicKey(HexUtil.hexToBytes("038871c219368549f7765f94c0b7b3046612f08e626771e98e235f4abb7ae363b9")), 3 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7360")), 4 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a635616f14d731231")), 5 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686fd734322")), 6 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7363")), 7 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7364")), 8 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7365")), 9 },
                { ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7366")), 10 }
            };
            
            var protos = new SortedDictionary<PublicKey, IGeneratorProtocol>(new PublicKeyComparer());
            foreach (var p in participants)
                protos[p.Key] = new DefaultGeneratorProtocol(participants, p.Key);
            
            Console.WriteLine("Initializing protocol");
            
            var seed = "0xbadcab1e".HexToBytes();
            
            foreach (var p in participants)
                protos[p.Key].Initialize(seed);
            
            BiprimalityTestResult biprimalityTestResult = null;
            int trie = 0;
            while (true)
            {
//                Console.WriteLine("Generating shares");

                Console.CursorLeft = 0;
                Console.Write("Try: " + trie);
                ++trie;
                
                var shares = new SortedDictionary<PublicKey, IDictionary<PublicKey, BgwPublicParams>>(new PublicKeyComparer());
                foreach (var p in participants)
                    shares[p.Key] = protos[p.Key].GenerateShare();
                var flipped = new SortedDictionary<PublicKey, IDictionary<PublicKey, BgwPublicParams>>(new PublicKeyComparer());
                foreach (var p in participants)
                {
                    var dict = new SortedDictionary<PublicKey, BgwPublicParams>(new PublicKeyComparer());
                    foreach (var pp in participants)
                    {
                        if (pp.Key.Equals(p.Key))
                            continue;
                        dict[pp.Key] = shares[p.Key][pp.Key];
                    }
                    flipped[p.Key] = dict;
                }
                
//                Console.WriteLine("Generating point");
            
                var points = new SortedDictionary<PublicKey, BGWNPoint>(new PublicKeyComparer());
                foreach (var p in participants)
                    points[p.Key] = protos[p.Key].GeneratePoint(shares[p.Key]);
                
//                Console.WriteLine("---------------------------------------");
                var proofs = new SortedDictionary<PublicKey, QiTestForRound>(new PublicKeyComparer());
                foreach (var p in participants)
                    proofs[p.Key] = protos[p.Key].GenerateProof(points);
//                Console.WriteLine("---------------------------------------");

                foreach (var p in participants)
                {
                    var test = protos[p.Key].ValidateProof(proofs);
                    if (!test.passes)
                        continue;
                    Console.WriteLine($"Biprimality test passed: {test}");
                    biprimalityTestResult = test;
                }
                if (biprimalityTestResult != null)
                    throw new Exception("SUCCESSS");
                
//                for (var i = 0; i < participants.Count; i++)
//                {
//                    try
//                    {
//                        var test = protos[i].ValidateProof(proofs);
//                        if (!test.passes)
//                            continue;
//                        Console.WriteLine($"Biprimality test passed: {test}");
//                        biprimalityTestResult = test;
//                    }
//                    catch (Exception e)
//                    {
//                        Console.Error.WriteLine(e.Message);
//                        break;
//                    }
//                }
//                if (biprimalityTestResult != null)
//                    break;
            }
//
//            Console.WriteLine("Generating derivations");
//            
//            var derivations = new IReadOnlyCollection<KeysDerivationPublicParameters>[participants.Count];
//            for (var i = 0; i < participants.Count; i++)
//                derivations[i] = protos[i].GenerateDerivation(biprimalityTestResult);
//            var flippedDerivation = new KeysDerivationPublicParameters[participants.Count][];
//            for (var i = 0; i < participants.Count; i++)
//                flippedDerivation[i] = new KeysDerivationPublicParameters[participants.Count];
//            
//            for (var i = 0; i < participants.Count; i++)
//            {
//                var si = derivations[i].ToArray();
//                for (var j = 0; j < si.Length; j++)
//                {
//                    flippedDerivation[j][i] = si[j];
//                }
//            }
//
//            Console.WriteLine("Generating thetas");
//            
//            var thetas = new ThetaPoint[participants.Count];
//            for (var i = 0; i < participants.Count; i++)
//                thetas[i] = protos[i].GenerateTheta(flippedDerivation[i]);
//
//            Console.WriteLine("Generating verification codes");
//            
//            var verificationKeys = new VerificationKey[participants.Count];
//            for (var i = 0; i < participants.Count; i++)
//                verificationKeys[i] = protos[i].GenerateVerification(thetas);
//
//            Console.WriteLine("Finalizing");
//            
//            for (var i = 0; i < participants.Count; i++)
//            {
//                var PrivateKey = protos[i].Finalize(verificationKeys);
//                Console.WriteLine("Private Key: " + HexUtil.bytesToHex(PrivateKey.toByteArray()));
//            }
        }
    }
}