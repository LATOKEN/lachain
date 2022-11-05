using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Serialization;
using MCL.BLS12_381.Net;
using Nethereum.RLP;

namespace Lachain.Consensus.Messages
{
    public class ProtocolRequest<TIdType, TInputType> : IInternalMessage 
        where TIdType : IProtocolIdentifier 
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
            var list = new List<byte[]>();
            list.Add(((int)ProtocolTypeMethods.GetProtocolType(From)).ToBytes().ToArray());
            list.Add(From.ToBytes().ToArray());
            list.Add(To.ToBytes().ToArray());

            switch (Input)
            {
                case null: 
                    break;
                case bool b:
                    list.Add((b ? 1: 0).ToBytes().ToArray());
                    break;
                case IByteSerializable b:
                    list.Add(b.ToBytes().ToArray());
                    break;
                default:
                    throw new InvalidOperationException("Unrecognized TInputType");
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ProtocolRequest<TIdType, TInputType> FromBytes(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}