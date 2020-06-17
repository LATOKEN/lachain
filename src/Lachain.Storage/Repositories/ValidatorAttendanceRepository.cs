using System;

namespace Lachain.Storage.Repositories
{
    public class ValidatorAttendanceRepository : IValidatorAttendanceRepository
    {
        private readonly IRocksDbContext _rocksDbContext;

        public ValidatorAttendanceRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }

        public void SaveState(byte[] keygenState)
        {
            var key = EntryPrefix.ValidatorAttendanceState.BuildPrefix();
            _rocksDbContext.Save(key, keygenState);
        }

        public byte[] LoadState()
        {
            var key = EntryPrefix.ValidatorAttendanceState.BuildPrefix();
            return _rocksDbContext.Get(key);
        }
    }
}