using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1.Pkcs;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.Consensus.TPKE
{
    class TPKESetup : AbstractProtocol
    {
        private TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        private TPKEPrivKey _privKey;
        private TPKEPubKey _pubKey;
        private int _n;
        private int _t;

        public TPKESetup(int n, int t, TPKESetupId tpkeSetupId, IConsensusBroadcaster broadcaster) : base(broadcaster)
        {
            _n = n;
            _t = t;
            _tpkeSetupId = tpkeSetupId;
        }

        public override IProtocolIdentifier Id { get; }
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.PrivateKey:
                        HandleTPKEPrivateKey(message.Validator, message.PrivateKey);
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
                    case ProtocolRequest<TPKESetupId, Object> request:
                        HandleInputMessage(request);
                        break;
                    case ProtocolResult<TPKESetupId, Tuple<TPKEPubKey, TPKEPrivKey>> _:
                        Terminated = true;
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleTPKEPrivateKey(Validator validator, TPKEPrivateKey tpkePrivateKey)
        {
            if (GetMyId() != (int) tpkePrivateKey.Id)
            {
               throw new Exception($"Id mismatch: expected {GetMyId()}, got {tpkePrivateKey.Id}"); 
            }

//            _privKey = new TPKEPrivKey(Fr.FromBytes(tpkePrivateKey), GetMyId());
//            _pubKey = new TPKEPubKey(G1.FromBytes());
        }


        private void HandleInputMessage(ProtocolRequest<TPKESetupId, Object> request)
        {
            throw new NotImplementedException(); 
        }
    }
}