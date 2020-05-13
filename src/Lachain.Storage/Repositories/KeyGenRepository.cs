using System;

namespace Lachain.Storage.Repositories
{
    public class KeyGenRepository : IKeyGenRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public KeyGenRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public void SaveKeyGenState(byte[] keygenState)
        {
            var key = EntryPrefix.KeyGenState.BuildPrefix();
            _rocksDbContext.Save(key, keygenState);
        }

        public byte[] LoadKeyGenState()
        {
            var key = EntryPrefix.KeyGenState.BuildPrefix();
            return _rocksDbContext.Get(key);
        }
    }
}