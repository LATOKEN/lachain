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
        private EncryptedShare _result;
        private bool _receivedAlready = false;

        private readonly ILogger<MockReliableBroadcast> _logger =
            LoggerFactory.GetLoggerForClass<MockReliableBroadcast>();

        public MockReliableBroadcast(ReliableBroadcastId reliableReliableBroadcastId, IWallet wallet,
            IConsensusBroadcaster broadcaster)
            : base(wallet, reliableReliableBroadcastId, broadcaster)
        {
            _reliableBroadcastId = reliableReliableBroadcastId;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.EncryptedShare:
                        HandleEncryptedShare(message.Validator, message.EncryptedShare);
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
                switch (message)
                {
                    case ProtocolRequest<ReliableBroadcastId, EncryptedShare> request:
                        HandleInputMessage(request);
                        break;
                    case ProtocolResult<ReliableBroadcastId, EncryptedShare> _:
                        Terminate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<ReliableBroadcastId, EncryptedShare> request)
        {
            _requested = ResultStatus.Requested;
            CheckResult();
            var share = request.Input;
            if (share == null) return;
            var msg = new ConsensusMessage
            {
                Validator = new Validator
                {
                    Era = Id.Era,
                    ValidatorIndex = GetMyId()
                },
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

        private void HandleEncryptedShare(Validator messageValidator, TPKEEncryptedShareMessage messageEncryptedShare)
        {
            _logger.LogError($"Got message from {messageValidator.ValidatorIndex} in {_reliableBroadcastId}");
            if (_receivedAlready)
            {
                _logger.LogDebug(
                    $"Player {GetMyId()} at {_reliableBroadcastId}: double receive of message from {messageValidator}!");
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