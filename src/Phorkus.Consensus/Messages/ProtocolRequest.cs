namespace Phorkus.Consensus.Messages
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
    }
}