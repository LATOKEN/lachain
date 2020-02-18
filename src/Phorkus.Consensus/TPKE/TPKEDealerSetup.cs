using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Crypto.TPKE;
using Phorkus.Logger;
using Phorkus.Proto;

namespace Phorkus.Consensus.TPKE
{
    public class TPKEDealerSetup : AbstractProtocol
    {
        private const int DealerId = 0;
        private TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private PrivateKey? _privateKey;
        private PublicKey? _publicKey;
        private VerificationKey? _verificationKey;
        private Keys? _result;
        private readonly ILogger<TPKEDealerSetup> _logger = LoggerFactory.GetLoggerForClass<TPKEDealerSetup>();

        public TPKEDealerSetup(TPKESetupId tpkeSetupId, IWallet wallet, IConsensusBroadcaster broadcaster) : base(
            wallet, tpkeSetupId, broadcaster)
        {
            _tpkeSetupId = tpkeSetupId;
            _requested = ResultStatus.NotRequested;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.TpkeKeys:
                        HandlePrivateKey(message.Validator, message.TpkeKeys);
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
                    case ProtocolRequest<TPKESetupId, object> request:
                        HandleInputMessage(request);
                        break;
                    case ProtocolResult<TPKESetupId, Keys> _:
                        Terminate();
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void
            HandlePrivateKey(Validator validator, TPKEKeysMessage tpkeKeys)
        {
            _logger.LogDebug($"{GetMyId()}: Got private key!");
            if (GetMyId() != (int) tpkeKeys.Id)
            {
                throw new Exception($"Id mismatch: expected {GetMyId()}, got {tpkeKeys.Id}");
            }

            byte[] privEnc = tpkeKeys.PrivateKey.ToByteArray();
            _privateKey = new PrivateKey(Fr.FromBytes(privEnc), GetMyId());

            byte[] pubEnc = tpkeKeys.PublicKey.ToByteArray();
            _publicKey = new PublicKey(G1.FromBytes(pubEnc), F);

            _verificationKey = VerificationKey.FromProto(tpkeKeys.VerificationKey);
            _result = new Keys(_publicKey, _privateKey, _verificationKey);

            CheckResult();
        }


        private void HandleInputMessage(ProtocolRequest<TPKESetupId, Object> request)
        {
            _requested = ResultStatus.Requested;
            if (GetMyId() == DealerId)
                Deal();
            CheckResult();
        }

        private void Deal()
        {
            var P = new Fr[F];
            for (var i = 0; i < F; ++i)
            {
                P[i] = Fr.GetRandom();
            }

            var pubKey = new PublicKey(G1.Generator * P[0], F);

            var Zs = new List<G2>();
            for (var i = 0; i < N; ++i)
            {
                var at = Fr.FromInt(i + 1);
                var res = Fr.FromInt(0);
                var cur = Fr.FromInt(1);
                for (var j = 0; j < F; ++j)
                {
                    res += P[j] * cur;
                    cur *= at;
                }

                Zs.Add(G2.Generator * res);
            }

            var verificationKey = new VerificationKey(G1.Generator * P[0], F, Zs.ToArray());

            for (var i = 0; i < N; ++i)
            {
                var at = Fr.FromInt(i + 1);
                var res = Fr.FromInt(0);
                var cur = Fr.FromInt(1);
                for (var j = 0; j < F; ++j)
                {
                    res += P[j] * cur;
                    cur *= at;
                }

                var privKey = new PrivateKey(res, i);

                // todo add full serialziation for pub and priv key
                var msg = CreateTPKEPrivateKeyMessage(pubKey, privKey, verificationKey, i);
                Broadcaster.SendToValidator(msg, i);
            }
        }

        private ConsensusMessage CreateTPKEPrivateKeyMessage(PublicKey pubKey, PrivateKey privKey,
            VerificationKey verificationKey, int to)
        {
            var message = new ConsensusMessage
            {
                Validator = new Validator
                {
                    ValidatorIndex = GetMyId(),
                    Era = Id.Era
                },
                TpkeKeys = new TPKEKeysMessage
                {
                    PublicKey = ByteString.CopyFrom(G1.ToBytes(pubKey.Y)),
                    PrivateKey = ByteString.CopyFrom(Fr.ToBytes(privKey.x)),
                    VerificationKey = verificationKey.ToProto(),
                    Id = (ulong) to
                }
            };
            return message;
        }


        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<TPKESetupId, Keys>(_tpkeSetupId, _result));
        }
    }
}