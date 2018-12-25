using System;
using System.Collections.Generic;

namespace Phorkus.Storage.RocksDB
{
    public interface IRocksDbContext : IDisposable
    {   
        byte[] Get(byte[] key);

        IDictionary<byte[], byte[]> GetMany(IEnumerable<byte[]> keys);

        void Save(byte[] key, byte[] content);

        void Save(IEnumerable<byte> key, IEnumerable<byte> content);
        
        void Delete(byte[] key);
    }
}