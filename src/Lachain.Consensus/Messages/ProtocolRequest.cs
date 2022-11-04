using System;

namespace Lachain.Consensus.Messages
{
    public class ProtocolRequest<TIdType, TInputType> : IInternalMessage where TIdType : IProtocolIdentifier
    {
        public ProtocolRequest(IProtocolIdentifier from, TIdType id, TInputType input)
        {
            From = from;
            To = id;
            Input = input;
        }

        public TInputType Input { get; }

        public IProtocolIdentifier From { get; }

        public IProtocolIdentifier To { get; }
        public byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public static ProtocolRequest<TIdType, TInputType> FromBytes(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}