using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;

namespace Lachain.Consensus
{
    public enum ProtocolType
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
        public static ProtocolType GetProtocolType(IProtocolIdentifier id)
        {
            return id switch
            {
                BinaryAgreementId _ => ProtocolType.BinaryAgreement,
                BinaryBroadcastId _ => ProtocolType.BinaryBroadcast,
                CoinId _ => ProtocolType.CommonCoin,
                CommonSubsetId _ => ProtocolType.CommonSubset,
                HoneyBadgerId _ => ProtocolType.HoneyBadger,
                ReliableBroadcastId _ => ProtocolType.ReliableBroadcast,
                RootProtocolId _ => ProtocolType.RootProtocol,
                _ => throw new ArgumentException("Unknown protocol type")
            };
        }
    }
}