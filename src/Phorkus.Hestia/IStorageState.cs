namespace Phorkus.Hestia
{
    public interface IStorageState
    {
        ulong CurrentVersion { get; }
        
        byte[] Get(byte[] key);
        ulong Add(byte[] key, byte[] value);
        ulong AddOrUpdate(byte[] key, byte[] value);
        ulong Update(byte[] key, byte[] value);
        ulong Delete(byte[] key, out byte[] value);
        ulong TryDelete(byte[] key, out byte[] value);
        
        ulong Commit();
        ulong Cancel();
    }
}