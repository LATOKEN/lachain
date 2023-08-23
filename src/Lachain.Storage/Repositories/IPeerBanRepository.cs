namespace Lachain.Storage.Repositories
{
    public interface IPeerBanRepository
    {
        void AddBannedPeer(ulong cycle, byte[] publicKey);
        byte[] GetBannedPeers(ulong cycle);
        void RemoveCycle(ulong cycle);
        ulong GetLowestCycle();
        void RemoveBannedPeer(ulong cycle, byte[] publicKey);
        ulong GetLowestCycleForVote();
        void RemoveVotingCycle(ulong cycle);
        void RemoveVotesForPeer(ulong cycle, byte[] publicKey);
        byte[] GetVotersForBannedPeer(ulong cycle, byte[] publicKey);
        uint AddVoteForBannedPeer(ulong cycle, byte[] publicKey, byte[] newVoter);
        byte[] GetVotedPeers(ulong cycle);
    }
}