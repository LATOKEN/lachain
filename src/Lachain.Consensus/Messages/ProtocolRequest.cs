using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.BinaryAgreement;
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
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>();
            list.Add(((int)ProtocolTypeMethods.GetProtocolType(From)).ToBytes().ToArray());
            list.Add(From.ToByteArray().ToArray());
            list.Add(To.ToByteArray().ToArray());

            switch (Input)
            {
                case null: 
                    break;
                case bool b:
                    list.Add((b ? 1: 0).ToBytes().ToArray());
                    break;
                case IByteSerializable b:
                    list.Add(b.ToByteArray().ToArray());
                    break;
                case ISet<IByteSerializable> set:
                    list.Add(set.Count.ToBytes().ToArray());
                    foreach (var element in set)
                    {
                        list.Add(element.ToByteArray().ToArray());
                    }
                    break;
                default:
                    throw new InvalidOperationException("Unrecognized TInputType");
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ProtocolRequest<TIdType, TInputType> FromByteArray(byte[] bytes)
        {
            // switch (Type.GetTypeCode(typeof(TIdType)))
            // {
            //         var decoded = (RLPCollection) RLP.Decode(bytes.ToArray());
            //         var protocolType = (ProtocolType) decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            //         
            // }
            throw new NotImplementedException();
        }
    }
}