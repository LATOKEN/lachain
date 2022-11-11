using Lachain.Utility.Serialization;

namespace Lachain.Consensus.Messages
{
    public interface IInternalMessage: IByteSerializable
    {
        IProtocolIdentifier From { get; }
        IProtocolIdentifier? To { get; }

        ProtocolType GetProtocolType();
    }
}