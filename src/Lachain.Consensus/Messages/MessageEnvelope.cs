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
        public const string ProtocolRequest = "ProtocolRequest";
        public const string ProtocolResponse = "ProtocolResponse";
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
        public bool IsProtocolRequest => !(InternalMessage is null) && TypeString() == ProtocolRequest;
        public bool IsProtocolResponse => !(InternalMessage is null) && TypeString() == ProtocolResponse;

        public string TypeString()
        {
            if (External) return ExternalMessage!.PayloadCase.ToString();
            return InternalMessage!.GetType().GetGenericTypeDefinition().Name.Contains("Request")
                ? ProtocolRequest
                : ProtocolResponse;
        }

        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                ValidatorIndex.ToBytes().ToArray(),
                (External ? 1 : 0).ToBytes().ToArray()
            };

            if (External)
            {
                list.Add(ExternalMessage.ToByteArray());
            }
            else {
                list.Add( (IsProtocolRequest ? 1 : 0).ToBytes().ToArray());
                var protocolType = (int) InternalMessage.GetProtocolType();
                list.Add(protocolType.ToBytes().ToArray());
                list.Add(InternalMessage.ToByteArray());
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static MessageEnvelope FromByteArray(byte[] bytes)
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
                    message = GetProtocolRequestFromByteArray(protocolType, decoded[4].RLPData);
                }
                else if (isProtocolRequest == 0)
                {
                    message = GetProtocolResponseFromByteArray(protocolType, decoded[4].RLPData);
                }
                else
                {
                    throw new InvalidOperationException("Unknown MessageEnvelope Type");
                }
                return new MessageEnvelope(message, validatorIndex);
            }
        }
        
        private static IInternalMessage GetProtocolRequestFromByteArray(ProtocolType protocolType, byte[] bytes)
        {
            switch (protocolType)
            {
                case ProtocolType.BinaryAgreement:
                    return ProtocolRequest<BinaryAgreementId, bool>.FromByteArray(bytes);
                case ProtocolType.BinaryBroadcast:
                    return ProtocolRequest<BinaryBroadcastId, bool>.FromByteArray(bytes);
                case ProtocolType.CommonCoin:
                    return ProtocolRequest<CoinId, object?>.FromByteArray(bytes);
                case ProtocolType.CommonSubset:
                    return ProtocolRequest<CommonSubsetId, EncryptedShare>.FromByteArray(bytes);
                case ProtocolType.HoneyBadger:
                    return ProtocolRequest<HoneyBadgerId, IRawShare>.FromByteArray(bytes);
                case ProtocolType.ReliableBroadcast:
                    return ProtocolRequest<ReliableBroadcastId, EncryptedShare?>.FromByteArray(bytes);
                case ProtocolType.RootProtocol:
                    return ProtocolRequest<RootProtocolId, IBlockProducer>.FromByteArray(bytes);
                default:
                    throw new InvalidOperationException($"Unknown Protocol Type {protocolType}");
            }
        }
        private static IInternalMessage GetProtocolResponseFromByteArray(ProtocolType protocolType, byte[] bytes)
        {
            switch (protocolType)
            {
                case ProtocolType.BinaryAgreement:
                    return ProtocolResult<BinaryAgreementId, bool>.FromByteArray(bytes);
                case ProtocolType.BinaryBroadcast:
                    return ProtocolResult<BinaryBroadcastId, BoolSet>.FromByteArray(bytes);
                case ProtocolType.CommonCoin:
                    return ProtocolResult<CoinId, CoinResult>.FromByteArray(bytes);
                case ProtocolType.CommonSubset:
                    return ProtocolResult<CommonSubsetId, ISet<EncryptedShare>>.FromByteArray(bytes);
                case ProtocolType.HoneyBadger:
                    return ProtocolResult<HoneyBadgerId, ISet<IRawShare>>.FromByteArray(bytes);
                case ProtocolType.ReliableBroadcast:
                    return ProtocolResult<ReliableBroadcastId, EncryptedShare?>.FromByteArray(bytes);
                case ProtocolType.RootProtocol:
                    return ProtocolResult<RootProtocolId, object?>.FromByteArray(bytes);
                default:
                throw new InvalidOperationException($"Unknown Protocol Type {protocolType}");
            }
        }

        protected bool Equals(MessageEnvelope other)
        {
            return Equals(ExternalMessage, other.ExternalMessage) && ValidatorIndex == other.ValidatorIndex && Equals(InternalMessage, other.InternalMessage);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((MessageEnvelope)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ExternalMessage, ValidatorIndex, InternalMessage);
        }
    }
}