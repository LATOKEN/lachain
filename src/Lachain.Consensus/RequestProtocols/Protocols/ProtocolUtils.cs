using System;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RequestProtocols.Messages;
using Lachain.Consensus.RootProtocol;

namespace Lachain.Consensus.RequestProtocols.Protocols
{
    public static class ProtocolUtils
    {
        public static ProtocolType GetProtocolType(IProtocolIdentifier id)
        {
            switch (id)
            {
                case RootProtocolId _:
                    return ProtocolType.Root;
                case HoneyBadgerId _:
                    return ProtocolType.HoneyBadger;
                case ReliableBroadcastId _:
                    return ProtocolType.ReliableBroadcast;
                case BinaryBroadcastId _:
                    return ProtocolType.BinaryBroadcast;
                case CoinId _:
                    return ProtocolType.CommonCoin;
                default:
                    throw new Exception($"Not implemented type for protocol id {id}");
            }
        }

        public static ProtocolType GetProtocolTypeForRequestType(RequestType requestType)
        {
            switch (requestType)
            {
                case RequestType.Aux:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Bval:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Coin:
                    return ProtocolType.CommonCoin;
                case RequestType.Conf:
                    return ProtocolType.BinaryBroadcast;
                case RequestType.Decrypted:
                    return ProtocolType.HoneyBadger;
                case RequestType.Echo:
                    return ProtocolType.ReliableBroadcast;
                case RequestType.Ready:
                    return ProtocolType.ReliableBroadcast;
                case RequestType.SignedHeader:
                    return ProtocolType.Root;
                case RequestType.Val:
                    return ProtocolType.ReliableBroadcast;
                default:
                    throw new Exception($"No protocol type for request type {requestType}");
            }
        }
    }
}