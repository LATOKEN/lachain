using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Utility.Serialization;
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
        public ProtocolType GetProtocolType()
        {
            return ProtocolTypeMethods.GetProtocolType(To);
        }

        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                ((int)ProtocolTypeMethods.GetProtocolType(From)).ToBytes().ToArray(),
                From.ToByteArray().ToArray(),
                ((int)ProtocolTypeMethods.GetProtocolType(To)).ToBytes().ToArray(),
                To.ToByteArray().ToArray()
            };

            switch (To)
            {
                case BinaryAgreementId _:
                    if (!(Input is bool binaryAgreementInput))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(( binaryAgreementInput ? 1: 0).ToBytes().ToArray());
                    break;
                case BinaryBroadcastId _:
                    if (!(Input is bool binaryBroadcastInput))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(( binaryBroadcastInput ? 1: 0).ToBytes().ToArray());
                    break;
                case CoinId _:
                    if (!(Input is null))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    break;
                case CommonSubsetId _:
                    if (!(Input is EncryptedShare commonSubsetInput))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    list.Add(commonSubsetInput.ToByteArray());
                    break;
                case HoneyBadgerId _:
                    if (!(Input is IRawShare honeyBadgerInput))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    list.Add(honeyBadgerInput.ToByteArray());
                    break;
                
                case ReliableBroadcastId _:
                    if (Input is null)
                        break;
                    if (!(Input is EncryptedShare reliableBroadcastInput))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    list.Add(reliableBroadcastInput.ToByteArray());
                    break;
                case RootProtocolId _:
                    if (!(Input is IBlockProducer))
                        throw new ArgumentException(
                            $"Unexpected Input type ({Input?.GetType()}) for ProtocolId {To.GetType()}");
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized TIdType {To.GetType()}");
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ProtocolRequest<TIdType, TInputType> FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            var fromType = (ProtocolType)decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var from = GetProtocolIdentifier(fromType, decoded[1].RLPData);
            
            var toType = (ProtocolType)decoded[2].RLPData.AsReadOnlySpan().ToInt32();
            var to = GetProtocolIdentifier(toType, decoded[3].RLPData);

            var input = decoded.Count >= 5 ? GetInputData(toType, decoded[4]?.RLPData): null;

            
            return new ProtocolRequest<TIdType, TInputType>(from, (TIdType) to, (TInputType) input );
        }

        private static object? GetInputData(ProtocolType toType, byte[]? bytes)
        {
            return toType switch
            {
                ProtocolType.BinaryAgreement => bytes.AsReadOnlySpan().ToInt32() == 1,
                ProtocolType.BinaryBroadcast => bytes.AsReadOnlySpan().ToInt32() == 1,
                ProtocolType.CommonCoin => null,
                ProtocolType.CommonSubset => EncryptedShare.FromBytes(bytes),
                ProtocolType.HoneyBadger => RawShare.FromByteArray(bytes),
                ProtocolType.ReliableBroadcast => bytes is null ? null : EncryptedShare.FromBytes(bytes),
                ProtocolType.RootProtocol => null,
                _ => throw new ArgumentOutOfRangeException($"Unrecognized Type of From {toType.ToString()}")
            };
        }

        private static IProtocolIdentifier GetProtocolIdentifier(ProtocolType type, byte[] bytes)
        {
            return type switch
            {
                ProtocolType.BinaryAgreement => BinaryAgreementId.FromByteArray(bytes),
                ProtocolType.BinaryBroadcast => BinaryBroadcastId.FromByteArray(bytes),
                ProtocolType.CommonCoin => CoinId.FromByteArray(bytes),
                ProtocolType.CommonSubset => CommonSubsetId.FromByteArray(bytes),
                ProtocolType.HoneyBadger => HoneyBadgerId.FromByteArray(bytes),
                ProtocolType.ReliableBroadcast => ReliableBroadcastId.FromByteArray(bytes),
                ProtocolType.RootProtocol => RootProtocolId.FromByteArray(bytes),
                _ => throw new ArgumentOutOfRangeException($"Unrecognized Type of From {type.ToString()}")
            };
        }

        protected bool Equals(ProtocolRequest<TIdType, TInputType> other)
        {
            return EqualityComparer<TInputType>.Default.Equals(Input, other.Input) && From.Equals(other.From) && To.Equals(other.To);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProtocolRequest<TIdType, TInputType>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Input, From, To);
        }
    }
}