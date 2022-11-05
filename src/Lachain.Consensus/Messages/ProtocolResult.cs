using System;

namespace Lachain.Consensus.Messages
{
    public class ProtocolResult<TIdType, TResultType> : IInternalMessage where TIdType : IProtocolIdentifier
    {
        public ProtocolResult(TIdType id, TResultType value)
        {
            Result = value;
            Id = id;
        }

        public TIdType Id { get; }

        public TResultType Result { get; }
        public IProtocolIdentifier From => Id;
        public IProtocolIdentifier? To => null;
        public byte[] ToBytes()
        {
            throw new NotImplementedException();
        }

        public static ProtocolResult<TIdType, TResultType> FromBytes(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}