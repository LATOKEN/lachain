using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Storage;
using NeoSharp.Core.Storage.Blockchain;

namespace NeoSharp.RocksDB.Repositories
{
    public class GlobalRepository : IGlobalRepository
    {
        private readonly byte[] _sysCurrentBlockKey = {(byte) DataEntryPrefix.SysCurrentBlock};
        private readonly byte[] _sysCurrentBlockHeaderKey = {(byte) DataEntryPrefix.SysCurrentHeader};
        private readonly byte[] _sysVersionKey = {(byte) DataEntryPrefix.SysVersion};

        private readonly IBinarySerializer _binarySerializer;
        private readonly IRocksDbContext _rocksDbContext;

        public GlobalRepository(
            IRocksDbContext rocksDbContext,
            IBinarySerializer binarySerializer)
        {
            _binarySerializer = binarySerializer ?? throw new ArgumentNullException(nameof(binarySerializer));
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public uint GetTotalBlockHeight()
        {
            var raw = _rocksDbContext.Get(_sysCurrentBlockKey);
            return raw == null ? uint.MinValue : BitConverter.ToUInt32(raw, 0);
        }

        public void SetTotalBlockHeight(uint height)
        {
            _rocksDbContext.Save(_sysCurrentBlockKey, BitConverter.GetBytes(height));
        }

        public uint GetTotalBlockHeaderHeight()
        {
            var raw = _rocksDbContext.Get(_sysCurrentBlockHeaderKey);
            return raw == null ? uint.MinValue : BitConverter.ToUInt32(raw, 0);
        }

        public void SetTotalBlockHeaderHeight(uint height)
        {
            _rocksDbContext.Save(_sysCurrentBlockHeaderKey, BitConverter.GetBytes(height));
        }

        public string GetVersion()
        {
            var raw = _rocksDbContext.Get(_sysVersionKey);
            return raw == null ? null : _binarySerializer.Deserialize<string>(raw);
        }

        public void SetVersion(string version)
        {
            _rocksDbContext.Save(_sysVersionKey, _binarySerializer.Serialize(version));
        }
    }
}