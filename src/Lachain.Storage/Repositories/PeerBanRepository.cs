using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Crypto;

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
        public void AddBannedPeer(ulong era, byte[] publicKey)
        {
            if (publicKey.Length != CryptoUtils.PublicKeyLength)
            {
                throw new Exception($"Invalid public key length {publicKey.Length}");
            }
            var peerList = GetBannedPeer(era);
            for (int i = 0 ; i < peerList.Length; i += CryptoUtils.PublicKeyLength)
            {
                if (peerList.Skip(i).Take(CryptoUtils.PublicKeyLength).SequenceEqual(publicKey))
                {
                    return;
                }
            }
            peerList = peerList.Concat(publicKey).ToArray();
            var prefix = EntryPrefix.BannedPeerListByEra.BuildPrefix(era);
            _dbContext.Save(prefix, peerList);
        }

        public byte[] GetBannedPeer(ulong era)
        {
            var prefix = EntryPrefix.BannedPeerListByEra.BuildPrefix(era);
            return _dbContext.Get(prefix) ?? new byte[0];
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveAllBannedPeer(ulong era)
        {
            var prefix = EntryPrefix.BannedPeerListByEra.BuildPrefix(era);
            _dbContext.Delete(prefix);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RemoveBannedPeer(ulong era, byte[] publicKey)
        {
            var peerList = GetBannedPeer(era);
            for (int i = 0 ; i < peerList.Length; i += CryptoUtils.PublicKeyLength)
            {
                if (peerList.Skip(i).Take(CryptoUtils.PublicKeyLength).SequenceEqual(publicKey))
                {
                    var remainingPeer = peerList.Skip(i + CryptoUtils.PublicKeyLength).ToArray();
                    peerList = peerList.Take(i).Concat(remainingPeer).ToArray();
                    var prefix = EntryPrefix.BannedPeerListByEra.BuildPrefix(era);
                    _dbContext.Save(prefix, peerList);
                    return;
                }
            }
        }
    }
}