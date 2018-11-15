using System;
using System.Threading;
using Phorkus.Core.Storage;

namespace Phorkus.RocksDB.Repositories
{
    public class GlobalRepository : IGlobalRepository
    {
        private readonly ReaderWriterLock _readerWriterLock;
        private readonly IRocksDbContext _rocksDbContext;

        public GlobalRepository(IRocksDbContext rocksDbContext)
        {
            _readerWriterLock = new ReaderWriterLock();
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public uint GetTotalBlockHeight()
        {
            throw new NotImplementedException();
            
            /*byte[] raw;
            _readerWriterLock.AcquireReaderLock(-1);
            try
            {
                raw = _rocksDbContext.Get(_sysCurrentBlockKey);
            }
            finally
            {
                _readerWriterLock.ReleaseReaderLock();
            }
            return raw == null ? uint.MinValue : BitConverter.ToUInt32(raw, 0);*/
        }

        public void SetTotalBlockHeight(uint height)
        {
            throw new NotImplementedException();
            /*_rocksDbContext.Save(_sysCurrentBlockKey, BitConverter.GetBytes(height)); */
        }

        public uint GetTotalBlockHeaderHeight()
        {
            throw new NotImplementedException();
            /*var raw = _rocksDbContext.Get(_sysCurrentBlockHeaderKey);
            return raw == null ? uint.MinValue : BitConverter.ToUInt32(raw, 0);*/
        }
        
        public void SetTotalBlockHeaderHeight(uint height)
        {
            throw new NotImplementedException();
            /*_rocksDbContext.Save(_sysCurrentBlockHeaderKey, BitConverter.GetBytes(height));*/
        }
    }
}