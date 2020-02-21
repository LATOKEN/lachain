using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Org.BouncyCastle.Crypto.Digests;
using Phorkus.Proto;

namespace Phorkus.Crypto
{
    public static class HashUtils
    {
        public static byte[] Sha256(this string message)
        {
            return Encoding.UTF8.GetBytes(message).Sha256();
        }

        public static byte[] Sha256(this byte[] message, int offset, int count)
        {
            var hash = new Sha256Digest();
            hash.BlockUpdate(message, offset, count);

            var result = new byte[32];
            hash.DoFinal(result, 0);
            return result;
        }

        public static byte[] Sha256(this byte[] message)
        {
            return Sha256(message, 0, message.Length);
        }

        public static byte[] Ripemd160(this string message)
        {
            return Encoding.ASCII.GetBytes(message).Ripemd160();
        }

        public static byte[] Ripemd160(this IEnumerable<byte> message, int offset, int count)
        {
            var hash = new RipeMD160Digest();
            hash.BlockUpdate(message.ToArray(), offset, count);
            var result = new byte[20];
            hash.DoFinal(result, 0);
            return result;
        }

        public static byte[] Ripemd160(this IEnumerable<byte> message)
        {
            var messageArray = message.ToArray();
            return Ripemd160(messageArray, 0, messageArray.Length);
        }

        public static byte[] Ed25519(this byte[] message)
        {
            throw new NotImplementedException();
        }

        public static byte[] Murmur3(this byte[] message, uint seed)
        {
            using (var murmur = new Murmur3(seed))
                return murmur.ComputeHash(message);
        }

        public static uint Murmur32(this byte[] message, uint seed)
        {
            using (var murmur = new Murmur3(seed))
            {
                return BitConverter.ToUInt32(murmur.ComputeHash(message), 0);
            }
        }

        public static byte[] Keccak256(this IEnumerable<byte> message)
        {
            var bytes = message as byte[] ?? message.ToArray();
            var digest = new KeccakDigest(256);
            digest.BlockUpdate(bytes, 0, bytes.Length);
            var output = new byte[32];
            digest.DoFinal(output, 0);
            return output;
        }

        public static UInt256 ToHash256<T>(this T t) // TODO: wtf sha256?
            where T : IMessage<T>
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Sha256())
            };
        }

        public static UInt256 Hash(this BlockHeader header)
        {
            return header.ToByteArray().ToHash256();
        }
        
        public static byte[] HashBytes(this BlockHeader header)
        {
            return header.ToByteArray().ToHash256().Buffer.ToByteArray();
        }

        public static UInt160 ToHash160<T>(this T t)
            where T : IMessage<T>
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Ripemd160())
            };
        }

        public static UInt160 ToHash160(this IEnumerable<byte> buffer)
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(buffer.Ripemd160())
            };
        }

        public static UInt256 ToHash256(this byte[] buffer)
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(buffer.Sha256())
            };
        }
    }
}