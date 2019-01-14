namespace Phorkus.Storage.State
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
    }
}