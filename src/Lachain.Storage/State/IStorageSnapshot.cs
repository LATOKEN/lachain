using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface IStorageSnapshot : ISnapshot
    {
        UInt256 GetValue(UInt160 contract, UInt256 key);
        void SetValue(UInt160 contract, UInt256 key, UInt256 value);
        void DeleteValue(UInt160 contract, UInt256 key, out UInt256 value);
    }
}