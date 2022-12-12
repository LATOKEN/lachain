namespace Lachain.Storage.Repositories
{
    public interface IPeerBanRepository
    {
        void AddBannedPeer(ulong era, byte[] publicKey);
        byte[] GetBannedPeer(ulong era);
        void RemoveAllBannedPeer(ulong era);
        void RemoveBannedPeer(ulong era, byte[] publicKey);
    }
}