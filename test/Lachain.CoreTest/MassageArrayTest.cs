using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class MessageArrayTest
    {
        [Test]
        public void Test_MessageArray()
        {
            var publicKey = "0x02a3ec3502ef652a969613696ec98a7bfd2c7b87d20efed47b3af726285d197d3c".HexToBytes().ToPublicKey();
            var peersRaw = "0x020a2102867b79666bac79f845ed33f4b2b0a964f9ac5b9be676b82bd9a01edcdad424150a2102a3ec3502ef652a969613696ec98a7bfd2c7b87d20efed47b3af726285d197d3c".HexToBytes();
            Console.WriteLine($"Adding peer to list: {publicKey.ToHex()}");
            Console.WriteLine($"before {peersRaw.ToHex()}");
            var str = "";
            var peers = ToEcdsaPublicKeys(peersRaw);
            foreach (var peer in peers)
            {
                str += peer.ToHex() + ", ";
            }
            Console.WriteLine($"peers list {str}");
            Console.WriteLine($"before len {peers.Count()}");
            
            peers.Add(publicKey);
                
            Console.WriteLine($"after len {peers.Count()}");
            Console.WriteLine($"after {peers.ToByteArray().ToHex()}");
        }

        

        public static ICollection<ECDSAPublicKey> ToEcdsaPublicKeys(byte[] buffer, ulong limit = ulong.MaxValue)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var result = new List<ECDSAPublicKey>();
                for (var i = 0UL; i < length; i++)
                {
                    var el = reader.ReadBytes(35).Skip(2).ToArray();
                    result.Add(new ECDSAPublicKey {Buffer = ByteString.CopyFrom(el)});
                    Console.WriteLine(el.ToHex());
                }

                return result;
            }
        }
    }
    
    
}