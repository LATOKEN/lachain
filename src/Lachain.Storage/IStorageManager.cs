namespace Lachain.Storage
{
    public interface IStorageManager
    {
        ulong LatestCommitedVersion(uint repository);

        byte[]? Get(uint repository, ulong version, byte[] key);

        IStorageState GetLastState(uint repository);
        IStorageState GetState(uint repository, ulong version);

        VersionFactory GetVersionFactory();
    }
}