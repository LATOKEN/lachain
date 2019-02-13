using Phorkus.Proto;

namespace Phorkus.Storage.State
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
        UInt256 Hash { get; }
    }
}