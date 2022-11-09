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
using Org.BouncyCastle.Utilities.Collections;

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
        public byte[] ToByteArray()
        {
            var list = new List<byte[]>();
            list.Add(((int)ProtocolTypeMethods.GetProtocolType(From)).ToBytes().ToArray());
            list.Add(From.ToByteArray().ToArray());
            list.Add(To.ToByteArray().ToArray());
            
            switch (To)
            {
                case BinaryAgreementId _:
                    if (!(Result is bool binaryAgreementResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(( binaryAgreementResult ? 1: 0).ToBytes().ToArray());
                    break;
                case BinaryBroadcastId _:
                    if (!(Result is BoolSet binaryBroadcastResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(binaryBroadcastResult.ToByteArray());
                    break;
                case CoinId _:
                    if (!(Result is CoinResult coinResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    list.Add(coinResult.ToByteArray());
                    break;
                case CommonSubsetId _:
                    if (!(Result is ISet<EncryptedShare> commonSubsetResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(commonSubsetResult.Count.ToBytes().ToArray());
                    list.AddRange(commonSubsetResult.ToList().OrderBy(c => c.GetHashCode()).Select(share => share.ToByteArray()));
                    break;
                case HoneyBadgerId _:
                    if (!(Result is ISet<IRawShare> honeyBadgerResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    
                    list.Add(honeyBadgerResult.Count.ToBytes().ToArray());
                    list.AddRange(honeyBadgerResult.ToList().OrderBy(c => c.GetHashCode()).Select(share => share.ToByteArray()));
                    break;
                
                case ReliableBroadcastId _:
                    if (!(Result is EncryptedShare reliableBroadcastResult))
                        throw new ArgumentException(
                            $"Unexpected Result type ({Result?.GetType()}) for ProtocolId {To.GetType()}");
                    list.Add(reliableBroadcastResult.ToByteArray());
                    break;
                case RootProtocolId _:
                    if (!(Result is null))
                        throw new ArgumentException(
                            $"Unexpected Result type (not null) for ProtocolId {To.GetType()}");
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized TIdType {To.GetType()}");
            }
            
            return RLP.EncodeList(list.Select(RLP.EncodeElement).ToArray());
        }

        public static ProtocolResult<TIdType, TResultType> FromByteArray(byte[] bytes)
        {
            throw new NotImplementedException();
        }
    }
}