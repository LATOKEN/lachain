using System;
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;
using TrustedKeyGen = Lachain.Crypto.ThresholdSignature.TrustedKeyGen;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class CommonSubsetTest
    {
        private readonly Random _rnd = new Random();
        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _acs = null!;
        private BroadcastSimulator[] _broadcasters = null!;
        private ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>[] _resultInterceptors = null!;
        private IPrivateConsensusKeySet[] _privateKeys = null!;
        private IPublicConsensusKeySet _publicKeys = null!;

        private void SetUpAllHonest(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _acs = new IConsensusProtocol[n];
            _broadcasters = new BroadcastSimulator[n];
            _resultInterceptors = new ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>[n];
            _privateKeys = new IPrivateConsensusKeySet[n];
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            _publicKeys = new PublicConsensusKeySet(
                n, f, pubKeys,
                Enumerable.Range(0, n)
                    .Select(i => new ECDSAPublicKey {Buffer = ByteString.CopyFrom(i.ToBytes().ToArray())})
            );
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<CommonSubsetId, ISet<EncryptedShare>>();
                _privateKeys[i] = new PrivateConsensusKeySet(null!, null!, shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, false);
            }

            for (uint i = 0; i < n; ++i)
            {
                _acs[i] = new CommonSubset(new CommonSubsetId(0), _publicKeys, _broadcasters[i]);
                _broadcasters[i].RegisterProtocols(new[] {_acs[i], _resultInterceptors[i]});
            }
        }

        private void CheckOutput(
            int n, int f,
            EncryptedShare[] inputs, ISet<EncryptedShare>[] outputs, ISet<int>? faulty = null
        )
        {
            faulty ??= new HashSet<int>();
            Assert.True(faulty.Count <= f);
            var numberOfInputs = 0;
            for (var i = 0; i < n; ++i)
                if (!faulty.Contains(i) && inputs[i] != null)
                    numberOfInputs++;

            // insufficient number of inputs
            if (numberOfInputs < n - f)
                Assert.Pass();

            EncryptedShare[]? canon = null;

            // Validity
            for (var i = 0; i < n; ++i)
            {
                if (faulty.Contains(i)) continue;
                var set = outputs[i];
                foreach (var share in set) Assert.True(inputs.Contains(share));
                Assert.True(set.Count >= n - f);
                var cnt = set.Count(share => !faulty.Contains(share.Id));
                Assert.True(cnt >= n - 2 * f);
                // Agreement -- all correct nodes output the same
                if (canon == null)
                    canon = set.ToArray();
                else
                    Assert.True(canon.SequenceEqual(set.ToArray()));
            }
        }

        private void TestAllCommonSubset(int n, int f, DeliveryServiceMode mode = DeliveryServiceMode.TAKE_FIRST)
        {
            SetUpAllHonest(n, f);
            _deliveryService.Mode = mode;
            var inputs = new List<EncryptedShare>();
            var rnd = new byte[32];
            _rnd.NextBytes(rnd);
            for (var i = 0; i < n; ++i)
            {
                var testShare = new EncryptedShare(G1.Generator, rnd, G2.Generator, i);
                inputs.Add(testShare);
                _broadcasters[i].InternalRequest(new ProtocolRequest<CommonSubsetId, EncryptedShare>(
                    _resultInterceptors[i].Id, (_acs[i].Id as CommonSubsetId)!, testShare
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (_deliveryService.MutedPlayers.Contains(i)) continue;
                _acs[i].WaitResult();
            }

            _deliveryService.WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                _broadcasters[i].Terminate();
                foreach (var id in _broadcasters[i].Registry.Keys)
                {
                    Assert.NotNull(id);
                    var protocol = _broadcasters[i].GetProtocolById(id);
                    if (protocol == null) continue;
                    protocol.Terminate();
                    protocol.WaitFinish();
                }
            }

            var outputs = new List<ISet<EncryptedShare>>();
            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_acs[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, _resultInterceptors[i].Result.Count);
                Assert.AreEqual(n, _resultInterceptors[i].Result[0].Count);

                outputs.Add(_resultInterceptors[i].Result[0]);
            }

            CheckOutput(n, f, inputs.ToArray(), outputs.ToArray());
        }

        [Test]
        [Timeout(5000)]
        public void TestRandom_7_1()
        {
            TestAllCommonSubset(7, 1, DeliveryServiceMode.TAKE_RANDOM);
        }

        [Test]
        [Timeout(5000)]
        public void TestRandom_7_2()
        {
            TestAllCommonSubset(7, 2, DeliveryServiceMode.TAKE_RANDOM);
        }

        [Test]
        [Timeout(5000)]
        public void TestSimple_7_1()
        {
            TestAllCommonSubset(7, 1);
        }

        [Test]
        [Timeout(5000)]
        public void TestSimple_7_2()
        {
            TestAllCommonSubset(7, 2);
        }

        [Test]
        [Timeout(5000)]
        public void TestSimple_10_3()
        {
            TestAllCommonSubset(10, 3);
        }

        [Test]
        [Repeat(10)]
        [Timeout(5000)]
        public void TestRandom()
        {
            var n = _rnd.Next(4, 10);
            var f = _rnd.Next(1, (n - 1) / 3 + 1);
            var mode = _rnd.SelectRandom(Enum.GetValues(typeof(DeliveryServiceMode)).Cast<DeliveryServiceMode>());
            TestAllCommonSubset(n, f, mode);
        }
    }
}