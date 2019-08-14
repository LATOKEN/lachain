using System.Collections.Generic;
using Org.BouncyCastle.Crypto.Engines;
using Phorkus.Consensus;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public class PlayerSet
    {
        private readonly List<IConsensusBroadcaster> _broadcasters = new List<IConsensusBroadcaster>();

        public void AddPlayer(IConsensusBroadcaster player)
        {
            _broadcasters.Add(player);
        }

        public void BroadcastMessage(ConsensusMessage consensusMessage)
        {
            foreach (var t in _broadcasters)
            {
                t.Dispatch(consensusMessage);
            }
        }

        public void SendToPlayer(ConsensusMessage consensusMessage, int index)
        {
            _broadcasters[index].Dispatch(consensusMessage);
        }
    }
}