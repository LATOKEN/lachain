using System;
using System.Runtime.Serialization;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Proto;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.ConsensusTest
{
    public static class TestUtils
    {
        public static IPrivateConsensusKeySet EmptyWallet(int n, int f)
        {
            return new PrivateConsensusKeySet(null!, null!, null!);
        }


        public static ConsensusMessage GenerateBinaryBroadcastConsensusMessage()
        {
            var _broadcastId = new BinaryBroadcastId(2142, 42342, 13124312);
            var message = new ConsensusMessage
            {
                Bval = new BValMessage
                {
                    Agreement = _broadcastId.Agreement,
                    Epoch = _broadcastId.Epoch,
                    Value = true
                }
            };
            return message;
        }

        public static BinaryAgreementId GenerateBinaryAgreementId(Random random)
        {
            return new BinaryAgreementId(random.Next(), random.Next());
        }
        public static BinaryBroadcastId GenerateBinaryBroadcastId(Random random)
        {
            return new BinaryBroadcastId(random.Next(), random.Next(), random.Next());
        }
        public static CoinId GenerateCoinId(Random random)
        {
            return new CoinId(random.Next(), random.Next(), random.Next());
        }
        public static CommonSubsetId GenerateCommonSubsetId(Random random)
        {
            return new CommonSubsetId(random.Next());
        }
        public static HoneyBadgerId GenerateHoneyBadgerId(Random random)
        {
            return new HoneyBadgerId(random.Next());
        }
        public static ReliableBroadcastId GenerateReliableBroadcastId(Random random)
        {
            return new ReliableBroadcastId(random.Next(), random.Next());
        }
        public static RootProtocolId GenerateRootProtocolId(Random random)
        {
            return new RootProtocolId(random.Next());
        }

        public static BoolSet GenerateBoolSet(Random random)
        {
            var bs = new BoolSet();
            if (random.Next(0, 1) == 1)
                bs.Add(true);
            if (random.Next(0, 1) == 1)
                bs.Add(false);
            return bs;
        }
    }
}