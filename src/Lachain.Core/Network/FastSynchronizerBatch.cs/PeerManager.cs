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
        private Queue<Peer> _availableGoodPeers = new Queue<Peer>();
        private Queue<Peer> _availableBadPeers = new Queue<Peer>();
        private Queue<DateTime> _lastResult = new Queue<DateTime>();
        private IDictionary<Peer, bool> _isPeerBusy = new Dictionary<Peer, bool>();
        public PeerManager(List<string> urls)
        {
            foreach(var url in urls) _isPeerBusy[new Peer(url)] = false;
            foreach (var item in _isPeerBusy) _availableGoodPeers.Enqueue(item.Key);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryGetPeer(out Peer peer)
        {
            peer = null;
            if (_availableGoodPeers.Count + _availableBadPeers.Count == 0) return false;
            else if(_availableGoodPeers.Count>0) peer = _availableGoodPeers.Dequeue();
            else{
                TimeSpan time = DateTime.Now - _lastResult.Peek();
                if( time.TotalMilliseconds < 120.0*1000 ) return false;
                peer = _availableBadPeers.Dequeue();
                _lastResult.Dequeue();
            }
            _isPeerBusy[peer] = true;
            return true; 
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryFreePeer(Peer peer, int success = 1)
        {
            if(_isPeerBusy.TryGetValue(peer, out var isBusy))
            {
                if (isBusy == false) return false;
                if(success == 0)
                {
                    _availableBadPeers.Enqueue(peer);
                    _lastResult.Enqueue(DateTime.Now);
                }
                else _availableGoodPeers.Enqueue(peer);
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
