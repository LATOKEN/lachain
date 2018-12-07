namespace Phorkus.Hestia
{
    public interface IPersistentStorageManager
    {
        ulong LatestCommitedVersion(uint repository);

        byte[] Get(uint repository, ulong version, byte[] key);

        IStorageState GetLastState(uint repository);
        IStorageState GetState(uint repository, ulong version);
        void SetLastState(uint repositrory, IStorageState state);
    }
}