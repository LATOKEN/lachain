using System;
using System.Threading;
using Google.Protobuf;
using Phorkus.Proto;

namespace Phorkus.Storage.RocksDB.Repositories
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
        
        public ulong GetTotalBlockHeight()
        {
            return _GetGlobal()?.BlockHeight ?? 0;
        }
        
        public void SetTotalBlockHeight(ulong height)
        {
            _ChangeGlobal(global => { global.BlockHeight = height;
                return global;
            });
        }

        public ulong GetTotalBlockHeaderHeight()
        {
            return _GetGlobal()?.BlockHeaderHeight ?? 0;
        }
        
        public void SetTotalBlockHeaderHeight(ulong height)
        {
            _ChangeGlobal(global => { global.BlockHeaderHeight = height;
                return global;
            });
        }

        public bool GenesisBlockExists()
        {
            return _GetGlobal() != null;
        }

        public ThresholdKey GetShare()
        {
            throw new NotImplementedException();
        }

        public void SetShare(ThresholdKey thresholdKey)
        {
            throw new NotImplementedException();
        }

        public ulong GetBlockchainHeight(BlockchainType blockchainType)
        {
            throw new NotImplementedException();
        }

        public ulong SetBlockchainHeight(BlockchainType blockchainType)
        {
            throw new NotImplementedException();
        }

        private Global _GetGlobal()
        {
            _readerWriterLock.AcquireReaderLock(-1);
            try
            {
                return _GetGlobalUnsafe();
            }
            finally
            {
                _readerWriterLock.ReleaseReaderLock();
            }
        }

        private Global _GetGlobalUnsafe()
        {
            if (_cachedGlobal != null)
                return _cachedGlobal;
            var raw = _rocksDbContext.Get(EntryPrefix.Global.BuildPrefix());
            if (raw is null)
                return new Global();
            _cachedGlobal = Global.Parser.ParseFrom(raw);
            return _cachedGlobal;
        }
        
        private void _ChangeGlobal(Func<Global, Global> factory)
        {
            _readerWriterLock.AcquireWriterLock(-1);
            try
            {
                _ChangeGlobalUnsafe(factory(_GetGlobalUnsafe()));
            }
            finally
            {
                _readerWriterLock.ReleaseWriterLock();
            }
        }

        private void _ChangeGlobalUnsafe(Global nextGlobal)
        {
            _rocksDbContext.Save(EntryPrefix.Global.BuildPrefix(), nextGlobal.ToByteArray());
            var raw = _rocksDbContext.Get(EntryPrefix.Global.BuildPrefix());
            if (raw is null) return;
            _cachedGlobal = Global.Parser.ParseFrom(raw);
        }
        
        private Global _cachedGlobal;
    }
}