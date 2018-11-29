namespace Phorkus.Hestia
{
    public interface IStorageManager
    {
        ulong LatestCommitedVersion(uint repository);

        byte[] Get(uint repository, ulong version, byte[] key);

        IStorageState NewState(uint repository);
    }
}