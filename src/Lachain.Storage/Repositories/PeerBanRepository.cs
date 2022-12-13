using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Crypto;
using Lachain.Utility.Serialization;

namespace Lachain.Storage.Repositories
{
    public class PeerBanRepository : IPeerBanRepository
    {
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
            var prefix = EntryPrefix.BannedPeerListByCycle.BuildPrefix(cycle);
            _dbContext.Save(prefix, peerList);
        }

        public byte[] GetBannedPeers(ulong cycle)
        {
            var prefix = EntryPrefix.BannedPeerListByCycle.BuildPrefix(cycle);
            return _dbContext.Get(prefix) ?? new byte[0];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveCycle(ulong cycle)
        {
            var lowestCycle = GetLowestCycle();
            var atomicWrite = new RocksDbAtomicWrite(_dbContext);
            var prefix = EntryPrefix.BannedPeerListByCycle.BuildPrefix(cycle);
            atomicWrite.Delete(prefix);
            if (cycle >= lowestCycle)
            {
                prefix = EntryPrefix.BannedPeerLowestCycle.BuildPrefix();
                atomicWrite.Put(prefix, (cycle + 1).ToBytes().ToArray());
            }
            atomicWrite.Commit();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public ulong GetLowestCycle()
        {
            var prefix = EntryPrefix.BannedPeerLowestCycle.BuildPrefix();
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
                    var prefix = EntryPrefix.BannedPeerListByCycle.BuildPrefix(cycle);
                    _dbContext.Save(prefix, peerList);
                    return;
                }
            }
        }
    }
}