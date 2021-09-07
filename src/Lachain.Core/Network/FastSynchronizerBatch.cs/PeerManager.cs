using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lachain.Core.Network.FastSynchronizerBatch
{
    class PeerManager
    {
        private Queue<Peer> _availablePeers = new Queue<Peer>();
        private IDictionary<Peer, bool> _isPeerBusy = new Dictionary<Peer, bool>();
        public PeerManager(List<string> urls)
        {
            foreach(var url in urls) _isPeerBusy[new Peer(url)] = false;
            foreach (var item in _isPeerBusy) _availablePeers.Enqueue(item.Key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetPeer(out Peer peer)
        {
            peer = null;
            if (_availablePeers.Count == 0) return false;
            peer = _availablePeers.Dequeue();
            _isPeerBusy[peer] = true;
            return true; 
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryFreePeer(Peer peer)
        {
            if(_isPeerBusy.TryGetValue(peer, out var isBusy))
            {
                if (isBusy == false) return false;
                _availablePeers.Enqueue(peer);
                _isPeerBusy[peer] = false;
                return true;
            }
            return false;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public int GetTotalPeerCount()
        {
            return _isPeerBusy.Count;
        }
    }
}
