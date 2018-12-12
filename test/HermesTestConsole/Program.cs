using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using NBitcoin;
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
            var publicKeys = new[]
            {
                ToPublicKey(HexUtil.hexToBytes("02affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5")),
                ToPublicKey(HexUtil.hexToBytes("0252b662232efa6affe522a78fbe06df7bb5809db64a165cffa1dbb3154722389a")),
                ToPublicKey(HexUtil.hexToBytes("038871c219368549f7765f94c0b7b3046612f08e626771e98e235f4abb7ae363b9")),
                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7360")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a635616f14d731231")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686fd734322")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7363")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7364")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7365")),
//                ToPublicKey(HexUtil.hexToBytes("03948f774e1bb92cebe996b1b5ddbc74c9b5b3965d290a537a63561686f14d7366"))
            }.OrderBy(pk => pk, new PublicKeyComparer()).ToArray();
            
            BiprimalityTestResult biprimalityTestResult = null;

            var protos = new SortedDictionary<PublicKey, IGeneratorProtocol>();
            var participants = new SortedDictionary<PublicKey, int>();
            var proofs = new SortedDictionary<PublicKey, QiTestForRound>(new PublicKeyComparer());
            
            int count = 0;
            while (true)
            {
                Console.Write("Try: " + count);
                Console.CursorLeft = 0;
                
                if (biprimalityTestResult is null)
                {
                    if (count % 1000 == 0)
                    {
                        participants = new SortedDictionary<PublicKey, int>(new PublicKeyComparer());
                        for (var i = 0; i < publicKeys.Length; i++)
                            participants.Add(publicKeys[i], i + 1);
            
                        protos = new SortedDictionary<PublicKey, IGeneratorProtocol>(new PublicKeyComparer());
                        foreach (var p in participants)
                            protos[p.Key] = new DefaultGeneratorProtocol(participants, p.Key);
                    
                        var seed = "0xbadcab1e".HexToBytes();

                        DefaultGeneratorProtocol.protoParam = null;
                        foreach (var p in participants)
                            protos[p.Key].Initialize(seed);
                    }
                    
                    var shares = new SortedDictionary<PublicKey, IDictionary<PublicKey, BgwPublicParams>>(new PublicKeyComparer());
                    foreach (var p in participants)
                    {
                        var ss = protos[p.Key].GenerateShare();
                        foreach (var s in ss)
                        {
                            var sk = shares.GetValueOrDefault(s.Key,
                                new SortedDictionary<PublicKey, BgwPublicParams>(new PublicKeyComparer()));
                            sk.Add(p.Key, ss[s.Key]);
                            shares.AddOrReplace(s.Key, sk);
                        }
                    }
                    var points = new SortedDictionary<PublicKey, BGWNPoint>(new PublicKeyComparer());
                    foreach (var p in participants)
                        points[p.Key] = protos[p.Key].GeneratePoint(shares[p.Key]);
                    foreach (var p in participants)
                        proofs[p.Key] = protos[p.Key].GenerateProof(points);
                }

                var nextProofs = new SortedDictionary<PublicKey, QiTestForRound>(new PublicKeyComparer());
                foreach (var p in participants)
                {
                    var proof = protos[p.Key].ValidateProof(proofs, out biprimalityTestResult);
                    if (biprimalityTestResult == null)
                        continue;
                    if (biprimalityTestResult.passes)
                    {
                        Console.WriteLine($"Found valid N in {count} rounds");
                        Console.WriteLine($"Biprimality test passed: {biprimalityTestResult.N}");
                    }
                    nextProofs[p.Key] = proof;
                }
                proofs = nextProofs;
                
                if (biprimalityTestResult != null && biprimalityTestResult.passes)
                    break;
                
                ++count;
          }

            Console.WriteLine("Generating derivations");
            
            var derivations = new SortedDictionary<PublicKey, IDictionary<PublicKey, KeysDerivationPublicParameters>>(new PublicKeyComparer());

            foreach (var p in participants)
            {
                var dd = protos[p.Key].GenerateDerivation(biprimalityTestResult);
                foreach (var s in dd)
                {
                    var sk = derivations.GetValueOrDefault(s.Key,
                        new SortedDictionary<PublicKey, KeysDerivationPublicParameters>(new PublicKeyComparer()));
                    sk.Add(p.Key, dd[s.Key]);
                    derivations.AddOrReplace(s.Key, sk);
                }
            }
            
            Console.WriteLine("Generating thetas");
            
            var thetas = new SortedDictionary<PublicKey, ThetaPoint>(new PublicKeyComparer());
            foreach (var p in participants)
                thetas[p.Key] = protos[p.Key].GenerateTheta(derivations[p.Key]);

            Console.WriteLine("Generating verification codes");
            
            var verificationKeys = new SortedDictionary<PublicKey, VerificationKey>(new PublicKeyComparer());
            foreach (var p in participants)
                verificationKeys[p.Key] = protos[p.Key].GenerateVerification(thetas);

            Console.WriteLine("Finalizing");

            foreach (var p in participants)
            {
                var pk = protos[p.Key].Finalize(verificationKeys);
                Console.WriteLine("Private Key: " + HexUtil.bytesToHex(pk.toByteArray()));
            }
        }
    }
}