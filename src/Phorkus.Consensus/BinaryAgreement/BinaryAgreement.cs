using System;
using Phorkus.Proto;

namespace Phorkus.Consensus.BinaryAgreement
{
    class BinaryAgreement : IBinaryAgreement
    {
        private readonly IConsensusBroadcaster _consensusBroadcaster;
        private readonly bool _finished;

        public BinaryAgreement(IConsensusBroadcaster consensusBroadcaster)
        {
            _consensusBroadcaster = consensusBroadcaster;
            _finished = false;
        }
        
        public void ProposeValue(bool value)
        {
            _consensusBroadcaster.Broadcast(new ConsensusMessage()); // BVAL
            
            throw new NotImplementedException();
        }

        public bool IsFinished()
        {
            return _finished;
        }

        public event EventHandler<bool> AgreementReached;
        
        public void HandleMessage(ConsensusMessage message)
        {
            throw new NotImplementedException();
        }
    }
}