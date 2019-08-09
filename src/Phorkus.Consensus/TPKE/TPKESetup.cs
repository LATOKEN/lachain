using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1.Pkcs;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus.TPKE
{
    class TPKESetup : AbstractProtocol
    {
        private TPKESetupId _tpkeSetupId;
        private ResultStatus _requested;
        
        public override IProtocolIdentifier Id { get; }
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


        private void HandleInputMessage(ProtocolRequest<TPKESetupId, Object> request)
        {
            throw new NotImplementedException(); 
        }
    }
}