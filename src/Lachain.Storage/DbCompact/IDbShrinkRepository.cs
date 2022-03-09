namespace Lachain.Storage.DbCompact
{
    public interface IDbShrinkRepository
    {
        void Save(byte[] key, byte[] content, bool tryCommit = true);
        void Delete(byte[] key, bool tryCommit = true);
        void Commit();
        byte[]? Get(byte[] key);
    }
}