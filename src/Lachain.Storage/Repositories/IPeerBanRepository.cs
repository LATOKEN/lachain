namespace Lachain.Storage.Repositories
{
    public interface IPeerBanRepository
    {
        void AddBannedPeer(ulong cycle, byte[] publicKey);
        byte[] GetBannedPeers(ulong cycle);
        void RemoveCycle(ulong cycle);
        ulong GetLowestCycle();
        void RemoveBannedPeer(ulong cycle, byte[] publicKey);
    }
}