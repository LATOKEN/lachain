using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Crypto.Misc;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Org.BouncyCastle.Crypto.Digests;

namespace Lachain.Crypto
{
    public static class HashUtils
    {
        public static byte[] RipemdBytes(this IEnumerable<byte> message)
        {
            var hash = new RipeMD160Digest();
            var messageArray = message as byte[] ?? message.ToArray();
            hash.BlockUpdate(messageArray, 0, messageArray.Length);
            var result = new byte[20];
            hash.DoFinal(result, 0);
            return result;
        }

        public static UInt160 Ripemd(this IEnumerable<byte> buffer)
        {
            return buffer.RipemdBytes().ToUInt160();
        }

        public static byte[] KeccakBytes(this IEnumerable<byte> message)
        {
            var bytes = message as byte[] ?? message.ToArray();
            var digest = new KeccakDigest(256);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            var output = new byte[32];
            digest.DoFinal(output, 0);
            return output;
        }

        public static byte[] KeccakBytes<T>(this T t) where T : IMessage<T>
        {
            return t.ToByteArray().KeccakBytes();
        }

        public static UInt256 Keccak<T>(this T t) where T : IMessage<T>
        {
            return t.ToByteArray().KeccakBytes().ToUInt256();
        }

        public static UInt256 Keccak(this BlockHeader header)
        {
            
            byte[][] headerBytes =
            {
                header.PrevBlockHash.ToBytes(),
                header.StateHash.ToBytes(),
                header.MerkleRoot.ToBytes(),
                header.Index.ToBytes().ToArray(),
                header.Nonce.ToBytes().ToArray(),
            };

            return Nethereum.RLP.RLP.EncodeElementsAndList(headerBytes).Keccak();
        }

        public static UInt256 Keccak(this IEnumerable<byte> buffer)
        {
            return buffer.KeccakBytes().ToUInt256();
        }

        public static byte[] Sha256Bytes(this IEnumerable<byte> message)
        {
            var bytes = message as byte[] ?? message.ToArray();
            var digest = new Sha256Digest();
            digest.BlockUpdate(bytes, 0, bytes.Length);
            var output = new byte[32];
            digest.DoFinal(output, 0);
            return output;
        }

        public static UInt256 Sha256(this IEnumerable<byte> buffer)
        {
            return buffer.Sha256Bytes().ToUInt256();
        }

        public static byte[] Murmur3(this byte[] message, uint seed)
        {
            using var murmur = new Murmur3(seed);
            return murmur.ComputeHash(message);
        }

        public static uint Murmur32(this byte[] message, uint seed)
        {
            using var murmur = new Murmur3(seed);
            return BitConverter.ToUInt32(murmur.ComputeHash(message), 0);
        }
    }
}