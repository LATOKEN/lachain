using System;
using System.Collections.Generic;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;

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
        
        private bool?[] _binaryAgreementInput;
        private bool?[] _binaryAgreementResult;
        private bool _filledBinaryAgreements = false;
        private int _cntBinaryAgreementsCompleted = 0;

        private IShare[] _reliableBroadcastResult;

        // todo move broadcaster to AbstractProtocol
        private IConsensusBroadcaster _broadcaster;


        public override IProtocolIdentifier Id => _commonSubsetId;

        public CommonSubset(int n, int f, IConsensusBroadcaster broadcaster)
        {
            _n = n;
            _f = f;
            _broadcaster = broadcaster;
            
            _binaryAgreementInput = new bool?[n];
            _binaryAgreementResult = new bool?[n];
            
            _reliableBroadcastResult = new IShare[n];
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
                    case ProtocolResult<CommonSubsetId, ISet<IShare>> _:
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
            
            CheckResult();
        }


        private void HandleReliableBroadcast(ProtocolResult<ReliableBroadcastId, IShare> result)
        {
            int j = result.Id.ValidatorId;

            _reliableBroadcastResult[j] = result.Result;
                
            if (_binaryAgreementInput[j] == null)
            {
                _binaryAgreementInput[j] = true;
                // todo send true to BA_j
            }
            
            CheckCompletion();
            
            throw new NotImplementedException();
        }

        private void HandleBinaryAgreementResult(ProtocolResult<BinaryAgreementId, bool> result)
        {
            // todo check for double send of result
            ++_cntBinaryAgreementsCompleted;
            _binaryAgreementResult[result.Id.ValidatorId] = result.Result;

            if (!_filledBinaryAgreements && _cntBinaryAgreementsCompleted >= _n - _f)
            {
                for (var i = 0; i < _n; ++i)
                {
                    if (_binaryAgreementInput[i] == null)
                    {
                        _binaryAgreementInput[i] = false;
                        // todo send false to BA_i
                    }
                }
            }

            CheckCompletion();
            
            throw new NotImplementedException(); 
        }

        private void CheckCompletion()
        {
            if (_result != null) return;
            
            if (_cntBinaryAgreementsCompleted < _n) return;
            
            for (var i = 0; i < _n; ++i)
            {
                if (_binaryAgreementResult[i] == true)
                {
                    if (_reliableBroadcastResult[i] == null) return;
                }
            }
            
            _result = new HashSet<IShare>();
            
            for (var i = 0; i < _n; ++i)
            {
                if (_binaryAgreementResult[i] == true)
                {
                    _result.Add(_reliableBroadcastResult[i]);
                }
            }
            
            CheckResult();
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