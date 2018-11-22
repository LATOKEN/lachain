using System;
using System.Linq;
using System.Text;
using NeoSharp.Core;
using NeoSharp.Core.Models;
using NeoSharp.Core.Storage;
using NeoSharp.Cryptography;

namespace NeoSharp.RocksDB
{
    public static class BuildKeyExtensions
    {
        public static byte[] BuildPrefix(this EntryPrefix prefix, string key)
        {
            var hash = Crypto.Default.Hash160(Encoding.ASCII.GetBytes(key));
            return BuildPrefix(prefix, hash);
        }

        public static byte[] BuildPrefix(this EntryPrefix prefix, uint key)
        {
            var bytes = new byte[5];
            bytes[0] = (byte) prefix;
            bytes[1] = (byte) ((key >>  0) & 0xff);
            bytes[2] = (byte) ((key >>  8) & 0xff);
            bytes[3] = (byte) ((key >> 16) & 0xff);
            bytes[4] = (byte) ((key >> 24) & 0xff);
            return bytes;
        }
        
        public static byte[] BuildPrefix(this EntryPrefix prefix, UInt160 key)
        {
            return BuildPrefix(prefix, key.ToArray());
        }
        
        public static byte[] BuildPrefix(this EntryPrefix prefix, UInt256 key)
        {
            return BuildPrefix(prefix, key.ToArray());
        }
        
        public static byte[] BuildPrefix(this EntryPrefix prefix)
        {
            var bytes = new byte[1];
            bytes[0] = (byte) prefix;
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
        
       
        
        
        
        
        
        public static byte[] BuildDataTransactionKey(this UInt256 hash)
        {
            return DataEntryPrefix.DataTransaction.BuildKey(hash.ToArray());
        }

        public static byte[] BuildStateAccountKey(this UInt160 hash)
        {
            return DataEntryPrefix.StAccount.BuildKey(hash.ToArray());
        }

        public static byte[] BuildStateContractKey(this UInt160 hash)
        {
            return DataEntryPrefix.StContract.BuildKey(hash.ToArray());
        }

        public static byte[] BuildStateStorageKey(this StorageKey key)
        {
            return DataEntryPrefix.StStorage.BuildKey(key.ScriptHash.ToArray().Concat(key.Key).ToArray());
        }
        
        private static byte[] BuildKey(this DataEntryPrefix dataEntryPrefix, byte[] key)
        {
            var len = key.Length;
            var bytes = new byte[len + 1];

            bytes[0] = (byte) dataEntryPrefix;
            Array.Copy(key, 0, bytes, 1, len);

            return bytes;
        }
    }
}