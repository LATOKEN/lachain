using System;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.TPKE;
using Phorkus.Logger;
using Phorkus.Proto;

namespace Phorkus.Consensus.ReliableBroadcast
{
    public class MockReliableBroadcast : AbstractProtocol
    {
        private readonly ReliableBroadcastId _reliableBroadcastId;
        private ResultStatus _requested = ResultStatus.NotRequested;
        private EncryptedShare? _result;
        private bool _receivedAlready;

        private readonly ILogger<MockReliableBroadcast> _logger =
            LoggerFactory.GetLoggerForClass<MockReliableBroadcast>();

        public MockReliableBroadcast(ReliableBroadcastId reliableReliableBroadcastId, IPublicConsensusKeySet wallet,
            IConsensusBroadcaster broadcaster) : base(wallet, reliableReliableBroadcastId, broadcaster)
        {
            _reliableBroadcastId = reliableReliableBroadcastId;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.EncryptedShare:
                        HandleEncryptedShare(envelope.ValidatorIndex, message.EncryptedShare);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message)
                {
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare?> request:
                        HandleInputMessage(request);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare?> _:
                        Terminate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare?> request)
        {
            _requested = ResultStatus.Requested;
            CheckResult();
            var share = request.Input;
            if (share == null) return;
            var msg = new ConsensusMessage
            {
                EncryptedShare = new TPKEEncryptedShareMessage
                {
                    U = ByteString.CopyFrom(G1.ToBytes(share.U)),
                    V = ByteString.CopyFrom(share.V),
                    W = ByteString.CopyFrom(G2.ToBytes(share.W)),
                    Id = share.Id
                }
            };
            Broadcaster.Broadcast(msg);
        }

        private void HandleEncryptedShare(int validatorIndex, TPKEEncryptedShareMessage messageEncryptedShare)
        {
            _logger.LogDebug($"Got message from {validatorIndex} in {_reliableBroadcastId}");
            if (_receivedAlready)
            {
                _logger.LogDebug(
                    $"{_reliableBroadcastId}: double receive of message from {validatorIndex}!");
                return;
            }

            _receivedAlready = true;
            var U = G1.FromBytes(messageEncryptedShare.U.ToByteArray());
            var V = messageEncryptedShare.V.ToByteArray();
            var W = G2.FromBytes(messageEncryptedShare.W.ToByteArray());
            var id = messageEncryptedShare.Id;
            _result = new EncryptedShare(U, V, W, id);
            CheckResult();
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<ReliableBroadcastId, EncryptedShare>(_reliableBroadcastId, _result));
        }
    }
}