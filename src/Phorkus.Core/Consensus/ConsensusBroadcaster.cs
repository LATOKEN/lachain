using System.Collections.Generic;
using Phorkus.Core.Blockchain;
using Phorkus.Crypto;
using Phorkus.Networking;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public class ConsensusBroadcaster
    {
        private readonly IValidatorManager _validatorManager;
        private readonly INetworkContext _networkContext;

        public ConsensusBroadcaster(
            IValidatorManager validatorManager,
            INetworkContext networkContext)
        {
            _validatorManager = validatorManager;
            _networkContext = networkContext;
        }

        public IDictionary<PublicKey, BlockPrepareReply> PrepareBlock(BlockPrepareRequest request, KeyPair keyPair)
        {
            var result = new Dictionary<PublicKey, BlockPrepareReply>();
            foreach (var entry in _GetActivePeers())
            {
                var reply = entry.Value.ConsensusService.PrepareBlock(request, keyPair);
                if (reply is null)
                    continue;
                result.Add(entry.Key, reply);
            }
            return result;
        }
        
        public IDictionary<PublicKey, ChangeViewReply> ChangeView(ChangeViewRequest request, KeyPair keyPair)
        {
            var result = new Dictionary<PublicKey, ChangeViewReply>();
            foreach (var entry in _GetActivePeers())
            {
                var reply = entry.Value.ConsensusService.ChangeView(request, keyPair);
                if (reply is null)
                    continue;
                result.Add(entry.Key, reply);
            }
            return result;
        }
        
        private IDictionary<PublicKey, IRemotePeer> _GetActivePeers()
        {
            var result = new Dictionary<PublicKey, IRemotePeer>();
            foreach (var publicKey in _validatorManager.Validators)
            {
                var peer = _networkContext.GetPeerByPublicKey(publicKey);
                if (peer is null)
                    continue;
                result.Add(publicKey, peer);
            }
            return result;
        }
    }
}