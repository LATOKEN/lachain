namespace Phorkus.Storage.Repositories
{
    public interface IVersionRepository
    {
        ulong GetVersion(uint repository);
        void SetVersion(uint repository, ulong version);
    }
}