using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeoSharp.RocksDB
{
    public interface IRocksDbContext : IDisposable
    {
        byte[] Get(byte[] key);

        IDictionary<byte[], byte[]> GetMany(IEnumerable<byte[]> keys);

        void Save(byte[] key, byte[] content);

        void Delete(byte[] key);
    }
}