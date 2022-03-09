using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Crypto.TPKE;
using System.IO;
using System.Runtime.Serialization;

namespace Lachain.Consensus.CommonSubset
{
    [DataContract]
    public class CommonSubset : AbstractProtocol
    {
        [DataMember]
        private static readonly ILogger<CommonSubset> Logger = LoggerFactory.GetLoggerForClass<CommonSubset>();

        [DataMember]
        private readonly CommonSubsetId _commonSubsetId;
        [DataMember]
        private ResultStatus _requested;
        [DataMember]
        private ISet<EncryptedShare>? _result;
        [DataMember]
        private readonly bool?[] _binaryAgreementInput;
        [DataMember]
        private readonly bool?[] _binaryAgreementResult;
        [DataMember]
        private bool _filledBinaryAgreements;
        [DataMember]
        private int _cntBinaryAgreementsCompleted;
        [DataMember]
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
                    _lastMessage = "Failed to decode external message";
                    throw new ArgumentNullException();
                }
                switch (message.PayloadCase)
                {
                    default:
                        _lastMessage =
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol";
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
                        _lastMessage = "InputMessage";
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> _:
                        _lastMessage = "ProtocolResult";
                        Terminate();
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> result:
                        _lastMessage = "ReliableBroadcast";
                        HandleReliableBroadcast(result);
                        break;
                    case ProtocolResult<BinaryAgreementId, bool> result:
                        _lastMessage = "BinaryAgreementResult";
                        HandleBinaryAgreementResult(result);
                        break;
                    default:
                        _lastMessage = "CommonSubset protocol failed to handle internal message";
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
                // Logger.LogDebug("CheckCompletion(): result is null");
                return;
            }

            if (_cntBinaryAgreementsCompleted < N)
            {
                // Logger.LogDebug($"CheckCompletion(): _cntBinaryAgreementsCompleted: {_cntBinaryAgreementsCompleted}; N: {N}");
                return;
            }

            if (_binaryAgreementResult
                .Zip(_reliableBroadcastResult, (b, share) => b == true && share is null)
                .Any(x => x)
            )
            {
                // Logger.LogDebug($"CheckCompletion(): _binaryAgreementResult check failed");
                return;
            }

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
            // Logger.LogDebug($"{GetMyId()} ACS terminated.");
            Broadcaster.InternalResponse(
                new ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>(_commonSubsetId, _result));
        }

        public byte[] ToBytes()
        {
            using var ms = new MemoryStream();
            var serializer = new DataContractSerializer(typeof(CommonSubset));
            serializer.WriteObject(ms, this);

            return ms.ToArray();
        }

        public static CommonSubset? FromBytes(ReadOnlyMemory<byte> bytes)
        {
            if(bytes.ToArray() == null)
            {
                return default;
            }

            using var memStream = new MemoryStream(bytes.ToArray());
            var serializer = new DataContractSerializer(typeof(CommonSubset));
            var obj = (CommonSubset?)serializer.ReadObject(memStream);

            return obj;
        }
    }
}