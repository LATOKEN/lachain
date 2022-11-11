using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;
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
                bs = bs.Add(true);
            if (random.Next(0, 1) == 1)
                bs = bs.Add(false);
            return bs;
        }

        public static CoinResult GenerateCoinResult(Random random)
        {
            return new CoinResult(G2.Generator.ToBytes());
        }

        public static EncryptedShare? GenerateEncryptedShare(Random random, bool canBeNull)
        {
            if (canBeNull && random.Next(0, 1) == 1)
                return null;
            var rnd = new byte[32];
            random.NextBytes(rnd);
            var share = new EncryptedShare(G1.Generator, rnd, G2.Generator, random.Next());
            Assert.AreEqual(share, EncryptedShare.FromByteArray(share.ToByteArray()));
            return share;
        }

        public static IRawShare GenerateIRawShare(Random random)
        {
            var rnd = new byte[32];
            random.NextBytes(rnd);
            return new RawShare(rnd, random.Next());
        }

        public static ISet<EncryptedShare> GenerateSetOfEncryptedShare(Random random)
        {
            var count = random.Next(1, 10);
            var set = new HashSet<EncryptedShare>();
            for (var i = 0; i < count; i++)
            {
                set.Add(GenerateEncryptedShare(random, false));
            }

            return set;
        }
        public static ISet<IRawShare> GenerateSetOfIRawShare(Random random)
        {
            var count = random.Next(1, 10);
            var set = new HashSet<IRawShare>();
            for (var i = 0; i < count; i++)
            {
                set.Add(GenerateIRawShare(random));
            }

            return set;
        }
    }
}