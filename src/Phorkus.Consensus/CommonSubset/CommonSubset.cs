using System;
using System.Collections.Generic;
using System.Data;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Consensus.TPKE;

namespace Phorkus.Consensus.CommonSubset
{
    public class CommonSubset : AbstractProtocol
    {
        private CommonSubsetId _commonSubsetId;
        private ResultStatus _requested;
        private ISet<EncryptedShare> _result;
        
        private readonly bool?[] _binaryAgreementInput;
        private readonly bool?[] _binaryAgreementResult;
        private bool _filledBinaryAgreements = false;
        private int _cntBinaryAgreementsCompleted = 0;

        private readonly EncryptedShare[] _reliableBroadcastResult;


        public override IProtocolIdentifier Id => _commonSubsetId;

        public CommonSubset(CommonSubsetId commonSubsetId, IWallet wallet, IConsensusBroadcaster broadcaster): base(wallet, broadcaster)
        {
            _commonSubsetId = commonSubsetId;
            
            _binaryAgreementInput = new bool?[N];
            _binaryAgreementResult = new bool?[N];
            
            _reliableBroadcastResult = new EncryptedShare[N];
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
                    case ProtocolRequest<CommonSubsetId, EncryptedShare> commonSubsetRequested:
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> _:
                        Terminated = true;
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> result:
                        HandleReliableBroadcast(result);
                        break;
                    case ProtocolResult<BinaryAgreementId, bool> result:
                        HandleBinaryAgreementResult(result);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<CommonSubsetId, EncryptedShare> request)
        {
            _requested = ResultStatus.Requested;
            
            _broadcaster.InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>
                (Id, new ReliableBroadcastId(GetMyId(), (int) _commonSubsetId.Era), request.Input));

            for (var j = 0; j < N; ++j)
            {
                if (j != GetMyId())
                {
                    _broadcaster.InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare>(Id, 
                        new ReliableBroadcastId(j, (int) _commonSubsetId.Era), null));
                }
            }
            
            CheckResult();
        }

        private void SendInputToBinaryAgreement(int j)
        {
            if (!_binaryAgreementInput[j].HasValue)
                throw new NoNullAllowedException();
            
            var id = new BinaryAgreementId( _commonSubsetId.Era,  j);
            _broadcaster.InternalRequest(new ProtocolRequest<BinaryAgreementId, bool>(Id, id, _binaryAgreementInput[j].Value));
        }


        private void HandleReliableBroadcast(ProtocolResult<ReliableBroadcastId, EncryptedShare> result)
        {
            var j = result.Id.AssociatedValidatorId;

            _reliableBroadcastResult[j] = result.Result;
            if (_binaryAgreementInput[j] == null)
            {
                _binaryAgreementInput[j] = true;
                SendInputToBinaryAgreement(j);
            }
            
            CheckCompletion();
        }

        private void HandleBinaryAgreementResult(ProtocolResult<BinaryAgreementId, bool> result)
        {
            // todo check for double send of result
            ++_cntBinaryAgreementsCompleted;
            _binaryAgreementResult[result.Id.AssociatedValidatorId] = result.Result;

            if (!_filledBinaryAgreements && _cntBinaryAgreementsCompleted >= N - F)
            {
                _filledBinaryAgreements = true;
                for (var i = 0; i < N; ++i)
                {
                    if (_binaryAgreementInput[i] == null)
                    {
                        _binaryAgreementInput[i] = false;
                        SendInputToBinaryAgreement(i);
                    }
                }
            }

            CheckCompletion();
        }

        private void CheckCompletion()
        {
            if (_result != null) return;
            
            if (_cntBinaryAgreementsCompleted < N) return;
            
            for (var i = 0; i < N; ++i)
            {
                if (_binaryAgreementResult[i] == true)
                {
                    if (_reliableBroadcastResult[i] == null) return;
                }
            }
            
            _result = new HashSet<EncryptedShare>();
            
            for (var i = 0; i < N; ++i)
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
            Console.Error.WriteLine($"{GetMyId()} ACS terminated.");
            _broadcaster.InternalResponse(
                new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>(_commonSubsetId, _result));
        }
    }
}