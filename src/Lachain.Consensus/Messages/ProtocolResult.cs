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
using Lachain.Utility.Utils;
using Nethereum.RLP;

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
        public ProtocolType GetProtocolType()
        {
            return ProtocolTypeMethods.GetProtocolType(Id);
        }

        public byte[] ToByteArray()
        {
            var list = new List<byte[]>
            {
                ((int)ProtocolTypeMethods.GetProtocolType(Id)).ToBytes().ToArray(),
                Id.ToByteArray().ToArray()
            };

            switch (Id)
            {
                case BinaryAgreementId _:
                    if (!(Result is bool binaryAgreementResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    
                    list.Add(( binaryAgreementResult ? 1: 0).ToBytes().ToArray());
                    break;
                case BinaryBroadcastId _:
                    if (!(Result is BoolSet binaryBroadcastResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    
                    list.Add(binaryBroadcastResult.ToByteArray());
                    break;
                case CoinId _:
                    if (!(Result is CoinResult coinResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    list.Add(coinResult.ToByteArray());
                    break;
                case CommonSubsetId _:
                    if (!(Result is ISet<EncryptedShare> commonSubsetResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    
                    list.Add(commonSubsetResult.Count.ToBytes().ToArray());
                    list.AddRange(commonSubsetResult.ToList().OrderBy(c => c.GetHashCode()).Select(share => share.ToByteArray()));
                    break;
                case HoneyBadgerId _:
                    if (!(Result is ISet<IRawShare> honeyBadgerResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    
                    list.Add(honeyBadgerResult.Count.ToBytes().ToArray());
                    list.AddRange(honeyBadgerResult.ToList().OrderBy(c => c.GetHashCode()).Select(share => share.ToByteArray()));
                    break;
                
                case ReliableBroadcastId _:
                    if (!(Result is EncryptedShare reliableBroadcastResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {Id.GetType()}");
                    list.Add(reliableBroadcastResult.ToByteArray());
                    break;
                case RootProtocolId _:
                    if (!(Result is null))
                        throw new ArgumentException(
                            $"Unexpected Result type (not null) for ProtocolId {Id.GetType()}");
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized TIdType {Id.GetType()}");
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ProtocolResult<TIdType, TResultType> FromByteArray(byte[] bytes)
        {
            var decoded = (RLPCollection)RLP.Decode(bytes.ToArray());
            
            var toType = (ProtocolType)decoded[0].RLPData.AsReadOnlySpan().ToInt32();
            var from = GetProtocolIdentifier(toType, decoded[1].RLPData);

            var result = GetResultData(toType, decoded);

            return new ProtocolResult<TIdType, TResultType>((TIdType) from,(TResultType) result);
        }

        private static object? GetResultData(ProtocolType toType, RLPCollection decoded)
        {
            return toType switch
            {
                ProtocolType.BinaryAgreement => decoded[2].RLPData.AsReadOnlySpan().ToInt32() == 1,
                ProtocolType.BinaryBroadcast => BoolSet.FromByteArray(decoded[2].RLPData),
                ProtocolType.CommonCoin => CoinResult.FromByteArray(decoded[2].RLPData),
                ProtocolType.CommonSubset => GetSetOfEncryptedShareFromBytes(decoded),
                ProtocolType.HoneyBadger => GetSetOfIRawShareFromBytes(decoded),
                ProtocolType.ReliableBroadcast => EncryptedShare.FromBytes(decoded[2].RLPData),
                ProtocolType.RootProtocol => null,
                _ => throw new ArgumentOutOfRangeException($"Unrecognized Type of From {toType.ToString()}")
            };
        }

        private static ISet <IRawShare> GetSetOfIRawShareFromBytes(RLPCollection decoded)
        {
            var count = decoded[2].RLPData.AsReadOnlySpan().ToInt32();
            ISet <IRawShare> set = new HashSet<IRawShare>();
            for (var i = 0; i < count; i++)
            {
                var share = RawShare.FromByteArray(decoded[3+i].RLPData);
                set.Add(share);
            }

            return set;
        }

        private static ISet<EncryptedShare> GetSetOfEncryptedShareFromBytes(RLPCollection decoded)
        {
            var count = decoded[2].RLPData.AsReadOnlySpan().ToInt32();
            ISet <EncryptedShare> set = new HashSet<EncryptedShare>();
            for (var i = 0; i < count; i++)
            {
                var share = EncryptedShare.FromByteArray(decoded[3+i].RLPData);
                set.Add(share);
            }

            return set;
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

        protected bool Equals(ProtocolResult<TIdType, TResultType> other)
        {
            return EqualityComparer<TIdType>.Default.Equals(Id, other.Id) && ResultEquals(Result, other.Result);
        }

        private bool ResultEquals(TResultType result, TResultType otherResult)
        {
            if (result is null || otherResult is null) {
                return result is null && otherResult is null;
            }

            switch (result)
            {
                case ISet<EncryptedShare> encryptedShares:
                    return encryptedShares.SetEquals((ISet<EncryptedShare>) otherResult);
                case ISet<IRawShare> rawShares:
                    return rawShares.SetEquals((ISet<IRawShare>) otherResult);
                default:
                    return EqualityComparer<TResultType>.Default.Equals(result, otherResult);
                    
            }
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ProtocolResult<TIdType, TResultType>)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Result);
        }
    }
}