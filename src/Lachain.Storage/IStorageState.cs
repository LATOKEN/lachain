using System.Collections.Generic;
using Lachain.Proto;

namespace Lachain.Storage
{
    public interface IStorageState
    {
        ulong CurrentVersion { get; }
        
        byte[]? Get(byte[] key);
        ulong Add(byte[] key, byte[] value);
        ulong AddOrUpdate(byte[] key, byte[] value);
        ulong Update(byte[] key, byte[] value);
        ulong Delete(byte[] key, out byte[]? value);
        ulong TryDelete(byte[] key, out byte[]? value);
        
        IEnumerable<byte[]> Values { get; }
        
        UInt256 Hash { get; }
        
        ulong Commit();
        ulong Cancel();
    }
}