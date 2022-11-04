using Lachain.Proto;
using Lachain.Utility.Serialization;

namespace Lachain.Consensus.Messages
{
    public class MessageEnvelope : IByteSerializable
    {
        public const string PROTOCOL_REQUEST = "ProtocolRequest";
        public const string PROTOCOL_RESPONSE = "ProtocolResponse";
        public ConsensusMessage? ExternalMessage { get; }

        public int ValidatorIndex { get; }
        public IInternalMessage? InternalMessage { get; }

        public MessageEnvelope(ConsensusMessage msg, int validatorIndex)
        {
            ExternalMessage = msg;
            InternalMessage = null;
            ValidatorIndex = validatorIndex;
        }

        public MessageEnvelope(IInternalMessage msg, int validatorIndex)
        {
            InternalMessage = msg;
            ExternalMessage = null;
            ValidatorIndex = validatorIndex;
        }

        public bool External => !(ExternalMessage is null);

        public string TypeString()
        {
            if (External) return ExternalMessage!.PayloadCase.ToString();
            return InternalMessage!.GetType().GetGenericTypeDefinition().Name.Contains("Request")
                ? PROTOCOL_REQUEST
                : PROTOCOL_RESPONSE;
        }

        public byte[] ToBytes()
        {
            throw new System.NotImplementedException();
        }

        public static MessageEnvelope FromBytes(byte[] bytes)
        {
            throw new System.NotImplementedException();
        }
    }
}