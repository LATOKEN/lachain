using System;
using System.Linq;
using NUnit.Framework;
using Phorkus.Consensus;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public class CommonCoinTest
    {
        private const int N = 7, F = 2;
        private ICommonCoin[] _coins;

        private class BroadcastSimulator : IConsensusBroadcaster
        {
            private readonly uint _sender;
            private readonly CommonCoinTest _test;

            public BroadcastSimulator(uint sender, CommonCoinTest test)
            {
                _sender = sender;
                _test = test;
            }

            public void Broadcast(ConsensusMessage message)
            {
                if (!message.PayloadCase.Equals(ConsensusMessage.PayloadOneofCase.CommonCoin))
                    throw new ArgumentException(nameof(message));
                message.Validator.ValidatorIndex = _sender;
                for (var i = 0; i < N; ++i)
                {
                    _test._coins[i]?.HandleMessage(message);
                }
            }
        }

        [SetUp]
        public void SetUp()
        {
            Mcl.Init();
            var keygen = new TrustedKeyGen(N, F, new Random(0x0badfee0));
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), F);
            _coins = new ICommonCoin[N];
            for (uint i = 0; i < N; ++i)
            {
                var broadcastSimulator = new BroadcastSimulator(i, this);
                _coins[i] = new CommonCoin(
                    pubKeys, shares[i], new CoinId(0, 0, 0), broadcastSimulator
                );
            }
        }

        [Test]
        public void TestAllHonest()
        {
            for (var i = 0; i < N; ++i)
            {
                var copyI = i;
                _coins[i].CoinTossed += (sender, b) =>
                {
                    Console.WriteLine($"{copyI}-th coin is {(b ? 1 : 0)}");
                };
            }

            for (var i = 0; i < N; ++i)
            {
                _coins[i].RequestCoin();
            }
        }
    }
}