using System;
using System.Collections.Generic;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus.CommonSubset
{
    class CommonSubset : AbstractProtocol
    {
        private CommonSubsetId _commonSubsetId;
        private ResultStatus _requested;
        private ISet<IShare> _result;
        private int _n;
        private int _f;
        private IShare _share;

        // todo move broadcaster to AbstractProtocol
        private IConsensusBroadcaster _broadcaster;


        public override IProtocolIdentifier Id => _commonSubsetId;

        public CommonSubset(int n, int f, IConsensusBroadcaster broadcaster)
        {
            _n = n;
            _f = f;
            _broadcaster = broadcaster;
        }
        
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<CommonSubsetId, IShare> commonSubsetRequested:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast requested");
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<BinaryBroadcastId, ISet<IShare>> _:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast completed");
                        Terminated = true;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<CommonSubsetId, IShare> request)
        {
            _share = request.Input;
            _requested = ResultStatus.Requested;
            
            // provide input to RBC_i
            
            // request results from RBC_j
            
            // request results from BA_j
            
            CheckResult();
        }


        private void HandleReliableBroadcast(ProtocolResult<ReliableBroadcastId, IShare> result)
        {
           throw new NotImplementedException();
        }

        private void HandleBinaryAgreementResult(ProtocolResult<BinaryAgreementId, bool> result)
        {
           throw new NotImplementedException(); 
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            _broadcaster.InternalResponse(
                new ProtocolResult<CommonSubsetId, ISet<IShare>>(_commonSubsetId, _result));
        }
    }
}