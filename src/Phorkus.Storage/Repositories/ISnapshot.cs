namespace Phorkus.Storage.Repositories
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
    }
}