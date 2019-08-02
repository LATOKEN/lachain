using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.Messages
{
    public class BroadcastCompleted : InternalMessage
    {
        public BoolSet Values { get; }
        public BinaryBroadcastId BroadcastId { get; }

        public BroadcastCompleted(BinaryBroadcastId broadcastId, BoolSet values)
        {
            Values = values;
            BroadcastId = broadcastId;
        }
    }
}