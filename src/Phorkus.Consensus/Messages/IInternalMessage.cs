namespace Phorkus.Consensus.Messages
{
    public interface IInternalMessage
    {
        IProtocolIdentifier From { get; }
        IProtocolIdentifier? To { get; }
    }
}