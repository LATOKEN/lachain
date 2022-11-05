using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Nethereum.RLP;

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
        public bool isProtocolRequest => !(InternalMessage is null) && TypeString() == PROTOCOL_REQUEST;
        public bool isProtocolResponse => !(InternalMessage is null) && TypeString() == PROTOCOL_RESPONSE;

        public string TypeString()
        {
            if (External) return ExternalMessage!.PayloadCase.ToString();
            return InternalMessage!.GetType().GetGenericTypeDefinition().Name.Contains("Request")
                ? PROTOCOL_REQUEST
                : PROTOCOL_RESPONSE;
        }

        public byte[] ToBytes()
        {
            var list = new List<byte[]>();
            list.Add(ValidatorIndex.ToBytes().ToArray());
            list.Add((External ? 1 : 0).ToBytes().ToArray());
            
            if (External)
            {
                list.Add(ExternalMessage.ToByteArray());
            }
            else {
                list.Add( (isProtocolRequest ? 1 : 0).ToBytes().ToArray());
                var protocolType = (int) ProtocolTypeMethods.GetProtocolType(InternalMessage.To);
                list.Add(protocolType.ToBytes().ToArray());
                list.Add(InternalMessage.ToBytes());
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static MessageEnvelope FromBytes(byte[] bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var validatorIndex = decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var external = decoded[1].RLPData.AsReadOnlySpan().ToInt32();

            if (external == 1)
            {
                var message = ConsensusMessage.Parser.ParseFrom(decoded[2].RLPData);
                return new MessageEnvelope(message, validatorIndex);
            }
            else
            {
                var isProtocolRequest = decoded[2].RLPData.AsReadOnlySpan().ToInt32();
                var protocolType = (ProtocolType)decoded[3].RLPData.AsReadOnlySpan().ToInt32();
                IInternalMessage message;

                if (isProtocolRequest == 1)
                {
                    message = GetProtocolRequestFromBytes(protocolType, decoded[4].RLPData);
                }
                else if (isProtocolRequest == 0)
                {
                    message = GetProtocolResponseFromBytes(protocolType, decoded[4].RLPData);
                }
                else
                {
                    throw new InvalidOperationException("Unknown MessageEnvelope Type");
                }
                return new MessageEnvelope(message, validatorIndex);
            }
        }
        
        private static IInternalMessage GetProtocolRequestFromBytes(ProtocolType protocolType, byte[] bytes)
        {switch (protocolType)
            {
                case ProtocolType.BinaryAgreement:
                    return ProtocolRequest<BinaryAgreementId, bool>.FromBytes(bytes);
                case ProtocolType.BinaryBroadcast:
                    return ProtocolRequest<BinaryBroadcastId, bool>.FromBytes(bytes);
                case ProtocolType.CommonCoin:
                    return ProtocolRequest<CoinId, object?>.FromBytes(bytes);
                case ProtocolType.CommonSubset:
                    return ProtocolRequest<CommonSubsetId, EncryptedShare>.FromBytes(bytes);
                case ProtocolType.HoneyBadger:
                    return ProtocolRequest<HoneyBadgerId, IRawShare>.FromBytes(bytes);
                case ProtocolType.ReliableBroadcast:
                    return ProtocolRequest<ReliableBroadcastId, EncryptedShare?>.FromBytes(bytes);
                case ProtocolType.RootProtocol:
                    return ProtocolRequest<RootProtocolId, IBlockProducer>.FromBytes(bytes);
                default:
                    throw new InvalidOperationException($"Unknown Protocol Type {protocolType}");
            }
        }
        private static IInternalMessage GetProtocolResponseFromBytes(ProtocolType protocolType, byte[] bytes)
        {
            switch (protocolType)
            {
                case ProtocolType.BinaryAgreement:
                    return ProtocolResult<BinaryAgreementId, bool>.FromBytes(bytes);
                case ProtocolType.BinaryBroadcast:
                    return ProtocolResult<BinaryBroadcastId, BoolSet>.FromBytes(bytes);
                case ProtocolType.CommonCoin:
                    return ProtocolResult<CoinId, CoinResult>.FromBytes(bytes);
                case ProtocolType.CommonSubset:
                    return ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>.FromBytes(bytes);
                case ProtocolType.HoneyBadger:
                    return ProtocolResult<HoneyBadgerId, EncryptedShare>.FromBytes(bytes);
                case ProtocolType.ReliableBroadcast:
                    return ProtocolResult<ReliableBroadcastId, EncryptedShare?>.FromBytes(bytes);
                case ProtocolType.RootProtocol:
                    return ProtocolResult<RootProtocolId, object?>.FromBytes(bytes);
                default:
                throw new InvalidOperationException($"Unknown Protocol Type {protocolType}");
            }
        }

    }
}