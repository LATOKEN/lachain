using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Repositories
{
    public class PeerRepository : IPeerRepository
    {
        private readonly IRocksDbContext _rocksDbContext;
        
        public PeerRepository(IRocksDbContext rocksDbContext)
        {
            _rocksDbContext = rocksDbContext ?? throw new ArgumentNullException(nameof(rocksDbContext));
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public Peer? GetPeerByPublicKey(ECDSAPublicKey publicKey)
        {
            Console.WriteLine($"GetPeerByPublicKey: {publicKey.ToHex()}");
            var prefix = EntryPrefix.PeerByPublicKey.BuildPrefix(publicKey.ToByteArray());
            var raw = _rocksDbContext.Get(prefix);
            return raw == null ? null : Peer.Parser.ParseFrom(raw);
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public ICollection<ECDSAPublicKey> GetPeerList()
        {
            var raw = _rocksDbContext.Get(EntryPrefix.PeerList.BuildPrefix());
            Console.WriteLine($"GetPeerList: raw: {raw.ToHex()}");
            var listStr = "";
            var array = raw.ToEcdsaPublicKeys().Select(x => { 
                listStr += x.ToHex() + ", ";
                return x;
            });
            Console.WriteLine($"GetPeerList: len: {array.Count()}");
            Console.WriteLine($"GetPeerList: {listStr}");
            return raw == null ? new List<ECDSAPublicKey>() : raw.ToEcdsaPublicKeys();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool RemovePeer(ECDSAPublicKey publicKey)
        {
            /* remove peer's public key from peer list */
            var raw = _rocksDbContext.Get(EntryPrefix.PeerList.BuildPrefix());
            if (raw == null)
                return false;
            var pool = raw.ToEcdsaPublicKeys();
            if (!pool.Remove(publicKey))
                return false;
            _rocksDbContext.Save(EntryPrefix.PeerList.BuildPrefix(), pool.ToByteArray());
            /* remove peer from storage */
            _rocksDbContext.Delete(EntryPrefix.PeerByPublicKey.BuildPrefix(publicKey.ToByteArray()));
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool AddOrUpdatePeer(ECDSAPublicKey publicKey, Peer peer)
        {
            Console.WriteLine($"AddOrUpdatePeer: {publicKey.ToHex()}");
            /* write peer to storage */
            var prefixTx = EntryPrefix.PeerByPublicKey.BuildPrefix(publicKey.ToByteArray());
            var rawPeer = _rocksDbContext.Get(prefixTx);
            if (rawPeer != null)
            {
                var currentTs = TimeUtils.CurrentTimeMillis() / 1000;
                var storedPeer = Peer.Parser.ParseFrom(rawPeer);
                if (storedPeer.Timestamp > peer.Timestamp)
                    return false;
                if (peer.Timestamp > currentTs
                    || peer.Timestamp < currentTs - 3600 * 24 * 5)
                    peer.Timestamp = (uint) (currentTs - 3600 * 24 * 5);
                else
                    peer.Timestamp -= 3600 * 2;

            }
            else
            {
                /* add peer to peer list */
                var peersRaw = _rocksDbContext.Get(EntryPrefix.PeerList.BuildPrefix());
                var peers = peersRaw != null ? peersRaw.ToEcdsaPublicKeys() : new List<ECDSAPublicKey>();
                
                peers.Add(publicKey);
                
                _rocksDbContext.Save(EntryPrefix.PeerList.BuildPrefix(), peers.ToByteArray());
            }
            _rocksDbContext.Save(prefixTx, peer.ToByteArray());
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool UpdatePeerTimestampIfExist(ECDSAPublicKey publicKey)
        {
            /* update peer */
            var prefixTx = EntryPrefix.PeerByPublicKey.BuildPrefix(publicKey.ToByteArray());
            var rawPeer = _rocksDbContext.Get(prefixTx);
            
            if (rawPeer == null)
                return false;
            
            var storedPeer = Peer.Parser.ParseFrom(rawPeer);
            storedPeer.Timestamp = (uint) (TimeUtils.CurrentTimeMillis() / 1000);
            _rocksDbContext.Save(prefixTx, storedPeer.ToByteArray());
            return true;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool ContainsPublicKey(ECDSAPublicKey publicKey)
        {
            var prefix = EntryPrefix.PeerByPublicKey.BuildPrefix(publicKey.ToByteArray());
            var raw = _rocksDbContext.Get(prefix);
            return raw != null;
        }
        
        [MethodImpl(MethodImplOptions.Synchronized)]
        public int GetPeersCount()
        {
            var peersRaw = _rocksDbContext.Get(EntryPrefix.PeerList.BuildPrefix());
            var peers = peersRaw != null ? peersRaw.ToEcdsaPublicKeys() : new List<ECDSAPublicKey>();
            return peers.Count;
        }
    }
}