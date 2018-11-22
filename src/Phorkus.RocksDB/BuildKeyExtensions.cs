using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.RocksDB
{
    public static class BuildKeyExtensions
    {
        public static byte[] BuildPrefix(this EntryPrefix prefix, string key)
        {
            return BuildPrefix(prefix, key.ToLower().Ripemd160());
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, ulong key)
        {
            var bytes = new byte[8 + 1];
            bytes[0] = (byte) prefix;
            bytes[1] = (byte) ((key >> 0) & 0xff);
            bytes[2] = (byte) ((key >> 8) & 0xff);
            bytes[3] = (byte) ((key >> 16) & 0xff);
            bytes[4] = (byte) ((key >> 24) & 0xff);
            bytes[5] = (byte) ((key >> 32) & 0xff);
            bytes[6] = (byte) ((key >> 40) & 0xff);
            bytes[7] = (byte) ((key >> 48) & 0xff);
            bytes[8] = (byte) ((key >> 56) & 0xff);
            return bytes;
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, uint key)
        {
            var bytes = new byte[4 + 1];
            bytes[0] = (byte) prefix;
            bytes[1] = (byte) ((key >> 0) & 0xff);
            bytes[2] = (byte) ((key >> 8) & 0xff);
            bytes[3] = (byte) ((key >> 16) & 0xff);
            bytes[4] = (byte) ((key >> 24) & 0xff);
            return bytes;
        }
        
        public static byte[] BuildPrefix(this EntryPrefix prefix, params UInt160[] keys)
        {
            if (keys.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(keys));
            var buffer = keys.Select(k => k.Buffer as IEnumerable<byte>).Aggregate((k1, k2) => k1.Concat(k2));
            return BuildPrefix(prefix, buffer);
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, UInt160 key)
        {
            return BuildPrefix(prefix, key.ToByteArray());
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, params UInt256[] keys)
        {
            if (keys.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(keys));
            var buffer = keys.Select(k => k.Buffer as IEnumerable<byte>).Aggregate((k1, k2) => k1.Concat(k2));
            return BuildPrefix(prefix, buffer);
        }
        
        public static byte[] BuildPrefix(this EntryPrefix prefix, UInt256 key)
        {
            return BuildPrefix(prefix, key.ToByteArray());
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix)
        {
            var bytes = new byte[1];
            bytes[0] = (byte) prefix;
            return bytes;
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, IEnumerable<byte> key)
        {
            var enumerable = key as byte[] ?? key.ToArray();
            var length = enumerable.Length;
            var bytes = new byte[length + 1];
            var number = (short) prefix;
            bytes[0] = (byte) (number >> 0 & 0xff);
            bytes[1] = (byte) (number >> 8 & 0xff);
            Array.Copy(enumerable.ToArray(), 0, bytes, 1, length);
            return bytes;
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, byte[] key)
        {
            var length = key.Length;
            var bytes = new byte[length + 1];
            var number = (short) prefix;
            bytes[0] = (byte) (number >> 0 & 0xff);
            bytes[1] = (byte) (number >> 8 & 0xff);
            Array.Copy(key, 0, bytes, 1, length);
            return bytes;
        }
    }
}