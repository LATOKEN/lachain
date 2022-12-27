using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Repositories
{
    public class PeerBanRepository : IPeerBanRepository
    {
        private static readonly ILogger<PeerBanRepository> Logger = LoggerFactory.GetLoggerForClass<PeerBanRepository>();
        private readonly IRocksDbContext _dbContext;
        public PeerBanRepository(IRocksDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddBannedPeer(ulong cycle, byte[] publicKey)
        {
            if (publicKey.Length != CryptoUtils.PublicKeyLength)
            {
                throw new Exception($"Invalid public key length {publicKey.Length}");
            }
            var peerList = GetBannedPeers(cycle);
            for (int i = 0 ; i < peerList.Length; i += CryptoUtils.PublicKeyLength)
            {
                if (peerList.Skip(i).Take(CryptoUtils.PublicKeyLength).SequenceEqual(publicKey))
                {
                    return;
                }
            }
            peerList = peerList.Concat(publicKey).ToArray();
            var prefix = BannedPeerListByCyclePrefix(cycle);
            _dbContext.Save(prefix, peerList);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] GetBannedPeers(ulong cycle)
        {
            var prefix = BannedPeerListByCyclePrefix(cycle);
            return _dbContext.Get(prefix) ?? new byte[0];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveCycle(ulong cycle)
        {
            var lowestCycle = GetLowestCycle();
            var atomicWrite = new RocksDbAtomicWrite(_dbContext);
            var prefix = BannedPeerListByCyclePrefix(cycle);
            atomicWrite.Delete(prefix);
            if (cycle >= lowestCycle)
            {
                prefix = BannedPeerLowestCyclePrefix();
                atomicWrite.Put(prefix, (cycle + 1).ToBytes().ToArray());
            }
            atomicWrite.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetLowestCycle()
        {
            var prefix = BannedPeerLowestCyclePrefix();
            var rawBytes = _dbContext.Get(prefix);
            if (rawBytes is null || rawBytes.Length == 0) return 0;
            return BitConverter.ToUInt64(rawBytes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveBannedPeer(ulong cycle, byte[] publicKey)
        {
            var peerList = GetBannedPeers(cycle);
            for (int i = 0 ; i < peerList.Length; i += CryptoUtils.PublicKeyLength)
            {
                if (peerList.Skip(i).Take(CryptoUtils.PublicKeyLength).SequenceEqual(publicKey))
                {
                    var remainingPeer = peerList.Skip(i + CryptoUtils.PublicKeyLength).ToArray();
                    peerList = peerList.Take(i).Concat(remainingPeer).ToArray();
                    var prefix = BannedPeerListByCyclePrefix(cycle);
                    _dbContext.Save(prefix, peerList);
                    return;
                }
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetLowestCycleForVote()
        {
            var prefix = BannedPeerVoteLowestCyclePrefix();
            var rawBytes = _dbContext.Get(prefix);
            if (rawBytes is null || rawBytes.Length == 0) return 0;
            return BitConverter.ToUInt64(rawBytes);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveVotingCycle(ulong cycle)
        {
            var atomicWrite = new RocksDbAtomicWrite(_dbContext);
            var votedPeers = GetVotedPeers(cycle);
            var prefix = VotedPeerListByCyclePrefix(cycle);
            atomicWrite.Delete(prefix);
            for (int i = 0 ; i < votedPeers.Length; i += CryptoUtils.PublicKeyLength)
            {
                var publicKey = votedPeers.Skip(i).Take(CryptoUtils.PublicKeyLength).ToArray();
                prefix = BannedPeerVotersByCyclePrefix(cycle, publicKey);
                atomicWrite.Delete(prefix);
            }
            var lowestCycle = GetLowestCycleForVote();
            if (lowestCycle <= cycle)
            {
                prefix = BannedPeerVoteLowestCyclePrefix();
                atomicWrite.Put(prefix, (cycle + 1).ToBytes().ToArray());
            }
            atomicWrite.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveVotesForPeer(ulong cycle, byte[] publicKey)
        {
            var prefix = BannedPeerVotersByCyclePrefix(cycle, publicKey);
            _dbContext.Delete(prefix);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] GetVotersForBannedPeer(ulong cycle, byte[] publicKey)
        {
            var prefix = BannedPeerVotersByCyclePrefix(cycle, publicKey);
            return _dbContext.Get(prefix) ?? new byte[0];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public uint AddVoteForBannedPeer(ulong cycle, byte[] publicKey, byte[] newVoter)
        {
            if (publicKey.Length != CryptoUtils.PublicKeyLength)
            {
                throw new Exception($"Invalid public key length of banned peer {publicKey.Length}");
            }
            if (newVoter.Length != CryptoUtils.PublicKeyLength)
            {
                throw new Exception($"Invalid public key length of voter {newVoter.Length}");
            }
            var voters = GetVotersForBannedPeer(cycle, publicKey);
            uint votes = (uint) voters.Length / CryptoUtils.PublicKeyLength;
            for (int i = 0 ; i < voters.Length; i += CryptoUtils.PublicKeyLength)
            {
                if (voters.Skip(i).Take(CryptoUtils.PublicKeyLength).SequenceEqual(newVoter))
                {
                    Logger.LogWarning($"{newVoter.ToHex()} already voter to ban peer {publicKey.ToHex()} for cycle {cycle}");
                    return votes;
                }
            }
            voters = voters.Concat(newVoter).ToArray();
            var prefix = BannedPeerVotersByCyclePrefix(cycle, publicKey);
            var atomicWrite = new RocksDbAtomicWrite(_dbContext);
            atomicWrite.Put(prefix, voters);
            if (votes == 0)
            {
                prefix = VotedPeerListByCyclePrefix(cycle);
                var votedPeers = GetVotedPeers(cycle);
                votedPeers = votedPeers.Concat(publicKey).ToArray();
                atomicWrite.Put(prefix, votedPeers);
            }
            atomicWrite.Commit();
            return votes + 1;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] GetVotedPeers(ulong cycle)
        {
            var prefix = VotedPeerListByCyclePrefix(cycle);
            return _dbContext.Get(prefix) ?? new byte[0];
        }

        private byte[] VotedPeerListByCyclePrefix(ulong cycle)
        {
            return EntryPrefix.VotedPeerListByCycle.BuildPrefix(cycle.ToBytes());
        }

        private byte[] BannedPeerVoteLowestCyclePrefix()
        {
            return EntryPrefix.BannedPeerVoteLowestCycle.BuildPrefix();
        }

        private byte[] BannedPeerLowestCyclePrefix()
        {
            return EntryPrefix.BannedPeerLowestCycle.BuildPrefix();
        }

        private byte[] BannedPeerVotersByCyclePrefix(ulong cycle, byte[] publicKey)
        {
            return EntryPrefix.BannedPeerVotersByCycle.BuildPrefix(cycle.ToBytes().Concat(publicKey));
        }

        private byte[] BannedPeerListByCyclePrefix(ulong cycle)
        {
            return EntryPrefix.BannedPeerListByCycle.BuildPrefix(cycle);
        }
    }
}