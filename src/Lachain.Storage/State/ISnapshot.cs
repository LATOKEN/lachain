using Lachain.Proto;

namespace Lachain.Storage.State
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
        UInt256 Hash { get; }
    }
}