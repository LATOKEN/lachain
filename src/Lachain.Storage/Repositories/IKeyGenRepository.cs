namespace Lachain.Storage.Repositories
{
    public interface IKeyGenRepository
    {
        void SaveKeyGenState(byte[] keygenState);
        byte[] LoadKeyGenState();
    }
}