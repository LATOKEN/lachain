using System;
using RocksDbSharp;

namespace Lachain.Storage
{
    public class RocksDbAtomicWrite : IDisposable
    {
        private readonly WriteBatch _writeBatch;
        private readonly IRocksDbContext _context;
        private bool _committed;

        public RocksDbAtomicWrite(IRocksDbContext context)
        {
            _context = context;
            _writeBatch = new WriteBatch();
        }

        public void Put(byte[] key, byte[] value)
        {
            _writeBatch.Put(key, value);
        }

        public void Commit()
        {
            if (_committed) throw new Exception("Tried to commit already committed write");
            _context.SaveBatch(_writeBatch);
            _committed = true;
        }

        public void Dispose()
        {
            _writeBatch.Dispose();
        }

        public WriteBatch GetWriteBatch()
        {
           return _writeBatch;
        }
    }
}