using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;

namespace Lachain.Consensus
{
    public enum ProtocolType: int
    {
        BinaryAgreement = 1,
        BinaryBroadcast = 2,
        CommonCoin = 3,
        CommonSubset = 4,
        HoneyBadger = 5,
        ReliableBroadcast = 6,
        RootProtocol = 7,
    }

    public static class ProtocolTypeMethods
    {
        public static ProtocolType getProtocolType(IProtocolIdentifier id)
        {
            switch (id)
            {
                case BinaryAgreementId _:
                    return ProtocolType.BinaryAgreement;
                case BinaryBroadcastId _:
                    return ProtocolType.BinaryBroadcast;
                case CoinId _:
                    return ProtocolType.CommonCoin;
                case CommonSubsetId _:
                    return ProtocolType.CommonSubset;
                case HoneyBadgerId _:
                    return ProtocolType.HoneyBadger;
                case ReliableBroadcastId _:
                    return ProtocolType.ReliableBroadcast;
                case RootProtocolId _:
                    return ProtocolType.RootProtocol;
                default:
                    throw new ArgumentException("Unknown protocol type");
            }
        }
    }
}