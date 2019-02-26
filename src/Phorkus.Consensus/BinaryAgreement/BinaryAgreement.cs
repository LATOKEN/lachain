using System;
using Phorkus.Proto;

namespace Phorkus.Consensus.BinaryAgreement
{
    class BinaryAgreement : IBinaryAgreement
    {
        private readonly int _parties, _faulty;
        private readonly BinaryAgreementId _agreementId;
        private readonly IConsensusBroadcaster _consensusBroadcaster;
        private readonly IMessageDispatcher _dispatcher;
        private readonly bool _finished;
        private uint _currentEpoch = 0;
        private bool _estimate;

        public BinaryAgreement(
            int n, int f, BinaryAgreementId agreementId,
            IConsensusBroadcaster consensusBroadcaster, IMessageDispatcher dispatcher
        )
        {
            _parties = n;
            _faulty = f;
            _agreementId = agreementId;
            _consensusBroadcaster = consensusBroadcaster;
            _dispatcher = dispatcher;
            _finished = false;
        }

        public void ProposeValue(bool value)
        {
            _estimate = value;
            var broadcastId = new BinaryBroadcastId(_agreementId.Era, _agreementId.Agreement, _currentEpoch);
            IBinaryBroadcast broadcast = new BinaryBroadcast(_parties, _faulty, broadcastId, _consensusBroadcaster);
            _dispatcher.RegisterAlgorithm(broadcast, broadcastId);
            broadcast.BinValueAdded += BroadcastOnBinValueAdded;
        }

        private void BroadcastOnBinValueAdded(object sender, int e)
        {
            var broadcastId = (sender as BinaryBroadcast)?.Id as BinaryBroadcastId ??
                              throw new ArgumentException(
                                  $"bad sender {sender} has incorrect id {(sender as BinaryBroadcast)?.Id}");
            throw new NotImplementedException();
        }

        public bool IsFinished()
        {
            return _finished;
        }

        public event EventHandler<bool> AgreementReached;

        public IProtocolIdentifier Id => _agreementId;

        public void HandleMessage(ConsensusMessage message)
        {
            throw new NotImplementedException();
        }

        public event EventHandler Terminated;
    }
}