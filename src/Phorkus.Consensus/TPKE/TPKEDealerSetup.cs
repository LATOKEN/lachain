using System;
using System.Collections.Generic;
using Google.Protobuf;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Consensus.TPKE
{
    public class TPKEDealerSetup : AbstractProtocol
    {
        private const int DEALER_ID = 0;
        private TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private TPKEPrivKey _privKey;
        private TPKEPubKey _pubKey;
        private TPKEVerificationKey _verificationKey;
        private TPKEKeys _result;
        

        public TPKEDealerSetup(TPKESetupId tpkeSetupId, IWallet wallet, IConsensusBroadcaster broadcaster) : base(wallet, tpkeSetupId, broadcaster)
        {
            _tpkeSetupId = tpkeSetupId;
            _requested = ResultStatus.NotRequested;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
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
                    case ProtocolResult<TPKESetupId, TPKEKeys> _:
                        Terminated = true;
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
            Console.Error.WriteLine($"{GetMyId()}: Got private key!");
            if (GetMyId() != (int) tpkeKeys.Id)
            {
               throw new Exception($"Id mismatch: expected {GetMyId()}, got {tpkeKeys.Id}"); 
            }

            byte[] privEnc = tpkeKeys.PrivateKey.ToByteArray();
            _privKey = new TPKEPrivKey(Fr.FromBytes(privEnc), GetMyId());

            byte[] pubEnc = tpkeKeys.PublicKey.ToByteArray();
            _pubKey = new TPKEPubKey(G1.FromBytes(pubEnc), F);

            _verificationKey = TPKEVerificationKey.FromProto(tpkeKeys.VerificationKey);
            _result = new TPKEKeys(_pubKey, _privKey, _verificationKey);

            CheckResult();
        }


        private void HandleInputMessage(ProtocolRequest<TPKESetupId, Object> request)
        {
            _requested = ResultStatus.Requested;
            if (GetMyId() == DEALER_ID)
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
            
            var pubKey = new TPKEPubKey(G1.Generator * P[0], F);

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
            
            var verificationKey = new TPKEVerificationKey(G1.Generator * P[0], F, Zs.ToArray());

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
                
                var privKey = new TPKEPrivKey(res, i);
                
                // todo add full serialziation for pub and priv key
                var msg = CreateTPKEPrivateKeyMessage(pubKey, privKey, verificationKey, i);
                _broadcaster.SendToValidator(msg, i);
            }
        }
        
        private ConsensusMessage CreateTPKEPrivateKeyMessage(TPKEPubKey pubKey, TPKEPrivKey privKey, TPKEVerificationKey verificationKey, int to)
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
            _broadcaster.InternalResponse(
                new ProtocolResult<TPKESetupId, TPKEKeys>(_tpkeSetupId, _result));
        }
    }
}