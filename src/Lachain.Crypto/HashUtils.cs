using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Org.BouncyCastle.Crypto.Digests;
using Lachain.Proto;
using Lachain.Utility.JSON;

namespace Lachain.Crypto
{
    public static class HashUtils
    {
        public static byte[] RipemdBytes(this IEnumerable<byte> message, int offset, int count)
        {
            var hash = new RipeMD160Digest();
            hash.BlockUpdate(message.ToArray(), offset, count);
            var result = new byte[20];
            hash.DoFinal(result, 0);
            return result;
        }

        public static byte[] RipemdBytes(this IEnumerable<byte> message)
        {
            var messageArray = message.ToArray();
            return RipemdBytes(messageArray, 0, messageArray.Length);
        }

        public static UInt160 Ripemd(this IEnumerable<byte> buffer)
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(buffer.RipemdBytes())
            };
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
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().KeccakBytes())
            };
        }

        public static UInt256 KeccakForTx(UInt256 data)
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(data.Buffer.ToByteArray().KeccakBytes())
            };
        }

        public static UInt256 Keccak(this IEnumerable<byte> buffer)
        {
            return new UInt256 {Buffer = ByteString.CopyFrom(buffer.KeccakBytes())};
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
            return new UInt256 {Buffer = ByteString.CopyFrom(buffer.Sha256Bytes())};
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
        
        public static UInt256 GetRlpHash(Transaction t)
        {
            var nonce = t.Nonce == 0 ? Array.Empty<byte>() : new BigInteger(t.Nonce).ToByteArray().Reverse().ToArray();
            var ethTx = new Nethereum.Signer.TransactionChainId(
                nonce,
                new BigInteger(t.GasPrice).ToByteArray().Reverse().ToArray(),
                new BigInteger(t.GasLimit).ToByteArray().Reverse().ToArray(),
                t.To.Buffer.ToByteArray(),
                t.Value.Buffer.ToByteArray().Reverse().ToArray(),
                Array.Empty<byte>(),
                new BigInteger(1).ToByteArray().Reverse().ToArray(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>()
            );
            
            var rlp = ethTx.GetRLPEncodedRaw();
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(rlp)
            };
        }

        public static UInt256 ToHash256(Transaction t)
        {
            var rlp = GetRlpHash(t);
            return KeccakForTx(rlp);
        }
    }
}