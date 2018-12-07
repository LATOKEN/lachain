namespace Phorkus.Hestia.Repositories
{
    public interface ISnapshot
    {
        ulong Version { get; }
        void Commit();
    }
}