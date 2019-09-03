using System;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;


namespace Phorkus.Consensus.ReliableBroadcast
{
    public class MockReliableBroadcast : AbstractProtocol
    {
        private readonly ReliableBroadcastId _reliableBroadcastId;
        public override IProtocolIdentifier Id => _reliableBroadcastId;
        private ResultStatus _requested = ResultStatus.NotRequested;
        private EncryptedShare _result;

        
        public MockReliableBroadcast(ReliableBroadcastId reliableReliableBroadcastId, IWallet wallet, IConsensusBroadcaster broadcaster) 
            : base(wallet, broadcaster)
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
                        Terminated = true;
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
            _broadcaster.Broadcast(msg);
        }
        
        private void HandleEncryptedShare(Validator messageValidator, TPKEEncryptedShareMessage messageEncryptedShare)
        {
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
            _broadcaster.InternalResponse(
                new ProtocolResult<ReliableBroadcastId, EncryptedShare>(_reliableBroadcastId, _result));
        }
    }
}
