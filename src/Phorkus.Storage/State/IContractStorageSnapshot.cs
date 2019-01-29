using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface IContractStorageSnapshot : ISnapshot
    {
        UInt256 GetValue(UInt160 contract, UInt256 key);
        void SetValue(UInt160 contract, UInt256 key, UInt256 value);
    }
}