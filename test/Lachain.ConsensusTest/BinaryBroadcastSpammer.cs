using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.ConsensusTest
{
    public class BinaryBroadcastSpammer : BinaryBroadcast
    {
        private readonly BinaryBroadcastId _broadcastId;
        public BinaryBroadcastSpammer(
            BinaryBroadcastId broadcastId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster)
            : base(broadcastId, wallet, broadcaster)
        {
            _broadcastId = broadcastId;
        }

        public void SpamBVal(bool bval)
        {
            var b = bval ? 1 : 0;
            var msg = CreateBValMessage(b);
            for (int i = 0 ; i < N ; i++) Broadcaster.Broadcast(msg);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private ConsensusMessage CreateBValMessage(int value)
        {
            var message = new ConsensusMessage
            {
                Bval = new BValMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = value == 1
                }
            };
            return message;
        }
    }
}