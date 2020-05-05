using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Crypto.TPKE;

namespace Lachain.Consensus.CommonSubset
{
    public class CommonSubset : AbstractProtocol
    {
        private readonly CommonSubsetId _commonSubsetId;
        private ResultStatus _requested;
        private ISet<EncryptedShare>? _result;

        private readonly bool?[] _binaryAgreementInput;
        private readonly bool?[] _binaryAgreementResult;
        private bool _filledBinaryAgreements;
        private int _cntBinaryAgreementsCompleted;

        private readonly EncryptedShare?[] _reliableBroadcastResult;
        private readonly ILogger<CommonSubset> _logger = LoggerFactory.GetLoggerForClass<CommonSubset>();

        public CommonSubset(
            CommonSubsetId commonSubsetId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster
        ) : base(wallet, commonSubsetId, broadcaster)
        {
            _commonSubsetId = commonSubsetId;
            _binaryAgreementInput = new bool?[N];
            _binaryAgreementResult = new bool?[N];
            
            // var resultInterceptors = new ProtocolInvoker<ReliableBroadcastId, EncryptedShare>();
            // broadcaster.RegisterProtocols(new[] {_broadcasts[i], resultInterceptors});
            _reliableBroadcastResult = new EncryptedShare[N];
            _result = null;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
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
                        Console.Error.WriteLine(
                            "Thread {0} ID_ACS {1} HandleInputMessage() into ACS", Thread.CurrentThread.ManagedThreadId, GetMyId());
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
            Broadcaster.InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>
                (Id, new ReliableBroadcastId(GetMyId(), (int) _commonSubsetId.Era), request.Input));
            Console.Error.WriteLine(
                "Thread {0} ID_ACS {1} create RBC from HandleInputMessage", Thread.CurrentThread.ManagedThreadId, GetMyId());

            for (var j = 0; j < N; ++j)
            {
                if (j != GetMyId())
                {
                    Broadcaster.InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(Id,
                        new ReliableBroadcastId(j, (int) _commonSubsetId.Era), null));
                    Console.Error.WriteLine(
                        "Thread {0} ID_ACS {1} create RBC from HandleInputMessage", Thread.CurrentThread.ManagedThreadId, GetMyId());
                }
            }
            CheckResult();
        }

        private void SendInputToBinaryAgreement(int j)
        {
            if (_binaryAgreementInput[j] is null || !_binaryAgreementInput[j].HasValue)
                throw new NoNullAllowedException();

            var id = new BinaryAgreementId(_commonSubsetId.Era, j);
            _logger.LogDebug($"Sending input {_binaryAgreementInput[j]} to {id}");
            Broadcaster.InternalRequest(
                new ProtocolRequest<BinaryAgreementId, bool>(Id, id, _binaryAgreementInput[j].Value));
        }

        private void HandleReliableBroadcast(ProtocolResult<ReliableBroadcastId, EncryptedShare> result)
        {
            if(result.Result == null) 
                return;
            
            var j = result.Id.AssociatedValidatorId;
            _logger.LogDebug($"Player {GetMyId()} at {_commonSubsetId}: {j}-th RBC completed.");

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
            _logger.LogDebug($"Received {result.From} result: {result.Result}");
            // todo check for double send of result
            ++_cntBinaryAgreementsCompleted;
            _binaryAgreementResult[result.Id.AssociatedValidatorId] = result.Result;
            _logger.LogDebug(
                $"Player {GetMyId()} at {_commonSubsetId}: {result.Id.AssociatedValidatorId}-th BA completed.");

            if (!_filledBinaryAgreements && _cntBinaryAgreementsCompleted >= N - F)
            {
                _logger.LogDebug($"Sending 0 to all remaining BA");
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

            if (_binaryAgreementResult
                .Zip(_reliableBroadcastResult, (b, share) => b == true && share is null)
                .Any(x => x)
            ) return;

            _result = _binaryAgreementResult
                .Zip(_reliableBroadcastResult, (b, share) => (b, share))
                .Where(x => x.b == true)
                .Select(x => x.share ?? throw new Exception())
                .ToHashSet();
            
            
            CheckResult();
        }

        private void CheckResult()
        {
            if (_result == null) 
                return;
            if (_requested != ResultStatus.Requested) 
                return;
            _requested = ResultStatus.Sent;
            SetResult();
            _logger.LogDebug($"{GetMyId()} ACS terminated.");
            Broadcaster.InternalResponse(
                new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>(_commonSubsetId, _result));
        }
    }
}