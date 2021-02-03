using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Crypto.TPKE;

namespace Lachain.Consensus.CommonSubset
{
    public class CommonSubset : AbstractProtocol
    {
        private static readonly ILogger<CommonSubset> Logger = LoggerFactory.GetLoggerForClass<CommonSubset>();

        private readonly CommonSubsetId _commonSubsetId;
        private ResultStatus _requested;
        private ISet<EncryptedShare>? _result;

        private readonly bool?[] _binaryAgreementInput;
        private readonly bool?[] _binaryAgreementResult;
        private bool _filledBinaryAgreements;
        private int _cntBinaryAgreementsCompleted;

        private readonly EncryptedShare?[] _reliableBroadcastResult;

        public CommonSubset(
            CommonSubsetId commonSubsetId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster
        ) : base(wallet, commonSubsetId, broadcaster)
        {
            _commonSubsetId = commonSubsetId;
            _binaryAgreementInput = new bool?[N];
            _binaryAgreementResult = new bool?[N];
            _reliableBroadcastResult = new EncryptedShare[N];
            _result = null;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = $"Failed to decode external message, {_cntBinaryAgreementsCompleted} BAs completed";
                    Logger.LogTrace( $"Failed to decode external message, {_cntBinaryAgreementsCompleted} BAs completed");
                    throw new ArgumentNullException();
                }
                switch (message.PayloadCase)
                {
                    default:
                        _lastMessage =
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace($"consensus message of type {message.PayloadCase} routed to CommonSubset protocol, {_cntBinaryAgreementsCompleted} BAs completed");
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol, {_cntBinaryAgreementsCompleted} BAs completed"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<CommonSubsetId, EncryptedShare> commonSubsetRequested:
                        _lastMessage = $"InputMessage, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace( _lastMessage);
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> _:
                        _lastMessage = $"ProtocolResult, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace( _lastMessage);
                        Terminate();
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> result:
                        _lastMessage = $"ReliableBroadcast, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace( _lastMessage);
                        HandleReliableBroadcast(result);
                        break;
                    case ProtocolResult<BinaryAgreementId, bool> result:
                        _lastMessage = $"BinaryAgreementResult, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace( _lastMessage);
                        HandleBinaryAgreementResult(result);
                        break;
                    default:
                        _lastMessage = $"CommonSubset protocol failed to handle internal message, {_cntBinaryAgreementsCompleted} BAs completed";
                        Logger.LogTrace( _lastMessage);
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

            for (var j = 0; j < N; ++j)
            {
                if (j != GetMyId())
                {
                    Broadcaster.InternalRequest(new ProtocolRequest<ReliableBroadcastId, EncryptedShare?>(Id,
                        new ReliableBroadcastId(j, (int) _commonSubsetId.Era), null));
                }
            }

            CheckResult();
        }

        private void SendInputToBinaryAgreement(int j)
        {
            if (_binaryAgreementInput[j] is null || !_binaryAgreementInput[j].HasValue)
                throw new NoNullAllowedException();

            var id = new BinaryAgreementId(_commonSubsetId.Era, j);
            Broadcaster.InternalRequest(
                new ProtocolRequest<BinaryAgreementId, bool>(Id, id, _binaryAgreementInput[j]!.Value)
            );
        }

        private void HandleReliableBroadcast(ProtocolResult<ReliableBroadcastId, EncryptedShare> result)
        {
            if (result.Result == null)
                return;

            var j = result.Id.SenderId;

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
            if(_binaryAgreementResult[result.Id.AssociatedValidatorId] == null)
                ++_cntBinaryAgreementsCompleted;
            _binaryAgreementResult[result.Id.AssociatedValidatorId] = result.Result;

            if (!_filledBinaryAgreements && _cntBinaryAgreementsCompleted >= N - F)
            {
                Logger.LogDebug($"Sending 0 to all remaining BA, _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}");
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
            if (_result != null)
            {
                _lastMessage = $"CheckCompletion(): result is null, _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}";
                Logger.LogDebug($"CheckCompletion(): result is null, _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}");
                return;
            }

            if (_cntBinaryAgreementsCompleted < N)
            {
                _lastMessage =
                    $"CheckCompletion(): _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}";
                Logger.LogDebug($"CheckCompletion(): _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}");
                return;
            }

            if (_binaryAgreementResult
                .Zip(_reliableBroadcastResult, (b, share) => b == true && share is null)
                .Any(x => x)
            )
            {
                _lastMessage =
                    $"CheckCompletion(): _binaryAgreementResult check failed, _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}";
                Logger.LogDebug($"CheckCompletion(): _binaryAgreementResult check failed, _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}");
                return;
            }

            _lastMessage =
                $"CheckCompletion(): _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}";
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
            Logger.LogDebug($"{GetMyId()} ACS terminated.");
            Broadcaster.InternalResponse(
                new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>(_commonSubsetId, _result));
        }
    }
}