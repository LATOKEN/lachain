using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1.Pkcs;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.Messages;

namespace Phorkus.Consensus.HoneyBadger
{
    class HoneyBadger : AbstractProtocol
    {
        private HoneyBadgerId _honeyBadgerId;
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
                    case ProtocolRequest<HoneyBadgerId, IRawShare> commonSubsetRequested:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast requested");
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> _:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast completed");
                        Terminated = true;
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<IShare>> result:
                        HandleCommonSubset(result);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private IShare EncryptRawShare(IRawShare rawShare)
        {
            // encrypt using TPKE
            throw new NotImplementedException();
        }

        private void HandleInputMessage(ProtocolRequest<HoneyBadgerId, IRawShare> request)
        {
            IShare _share = EncryptRawShare(request.Input);
            
            // send share to CommonSubset
            
            throw new NotImplementedException(); 
        }

        private void HandleCommonSubset(ProtocolResult<CommonSubsetId, ISet<IShare>> result)
        {
            
            throw new NotImplementedException();
        }
    }
}