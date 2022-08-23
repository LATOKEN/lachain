using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Lachain.Consensus;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;
using TestUtility = Lachain.UtilityTest.TestUtils;

namespace Lachain.ConsensusTest
{
    [TestFixture]
    public class HoneyBadgerTest
    {
        private static readonly ILogger<HoneyBadgerTest> Logger = LoggerFactory.GetLoggerForClass<HoneyBadgerTest>();
        private const int Era = 0;

        private readonly Random _rnd = new Random();
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private DeliveryService _deliveryService = null!;
        private IConsensusProtocol[] _broadcasts = null!;
        private IConsensusBroadcaster[] _broadcasters = null!;
        private ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[] _resultInterceptors = null!;
        private IPublicConsensusKeySet _publicKeys = null!;
        private IPrivateConsensusKeySet[] _privateKeys = null!;

        public void SetUp(int n, int f)
        {
            _deliveryService = new DeliveryService();
            _broadcasts = new IConsensusProtocol[n];
            _broadcasters = new IConsensusBroadcaster[n];
            _resultInterceptors = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>[n];
            var keygen = new TrustedKeyGen(n, f);
            var shares = keygen.GetPrivateShares().ToArray();
            var pubKeys = new PublicKeySet(shares.Select(share => share.GetPublicKeyShare()), f);
            var tpkeKeygen = new Crypto.TPKE.TrustedKeyGen(n, f);

            var ecdsaKeys = Enumerable.Range(0, n)
                .Select(i => Crypto.GenerateRandomBytes(32))
                .Select(x => x.ToPrivateKey())
                .Select(k => new EcdsaKeyPair(k))
                .ToArray();
            var tpkeVerificationKeys = Enumerable.Range(0, n)
                .Select(i => tpkeKeygen.GetVerificationPubKey(i)).ToArray();
            _publicKeys = new PublicConsensusKeySet(n, f, tpkeKeygen.GetPubKey(), tpkeVerificationKeys, pubKeys,
                ecdsaKeys.Select(k => k.PublicKey));
            _privateKeys = new IPrivateConsensusKeySet[n];
            for (var i = 0; i < n; ++i)
            {
                _resultInterceptors[i] = new ProtocolInvoker<HoneyBadgerId, ISet<IRawShare>>();
                _privateKeys[i] = new PrivateConsensusKeySet(ecdsaKeys[i], tpkeKeygen.GetPrivKey(i), shares[i]);
                _broadcasters[i] = new BroadcastSimulator(i, _publicKeys, _privateKeys[i], _deliveryService, true);
            }
        }

        private void SetUpAllHonest(int n, int f)
        {
            SetUp(n, f);
            for (uint i = 0; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, false, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpOneMalicious(int n, int f)
        {
            SetUp(n, f);
            _broadcasts[0] = new HoneyBadgerMalicious(
                new HoneyBadgerId(Era), _publicKeys, _privateKeys[0].TpkePrivateKey, false, _broadcasters[0]
            );
            _broadcasters[0].RegisterProtocols(new[] {_broadcasts[0], _resultInterceptors[0]});
            for (uint i = 1; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, false, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpOneSmartMalicious(int n, int f)
        {
            SetUp(n, f);
            _broadcasts[0] = new HoneyBadgerSmartMalicious(
                new HoneyBadgerId(Era), _publicKeys, _privateKeys[0].TpkePrivateKey, false, _broadcasters[0]
            );
            _broadcasters[0].RegisterProtocols(new[] {_broadcasts[0], _resultInterceptors[0]});
            for (uint i = 1; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, false, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
            }
        }

        private void SetUpSomeSilent(int n, int f, ISet<int> s)
        {
            SetUp(n, f);
            for (var i = 0; i < n; ++i)
            {
                _broadcasts[i] = new HoneyBadger(
                    new HoneyBadgerId(Era), _publicKeys, _privateKeys[i].TpkePrivateKey, false, _broadcasters[i]
                );
                _broadcasters[i].RegisterProtocols(new[] {_broadcasts[i], _resultInterceptors[i]});
                foreach (var j in s) (_broadcasters[i] as BroadcastSimulator)?.Silent(j);
            }
        }

        private void Stop(int n)
        {
            for (var i = 0 ; i < n ; i++)
            {
                _broadcasters[i].Terminate();
            }
        }

        [Test]
        public void TestAllHonest_7_2()
        {
            const int n = 7, f = 2;
            SetUpAllHonest(n, f);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i) _broadcasts[i].WaitFinish();
            for (var i = 0; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        public void TestSomeSilent_7_2()
        {
            const int n = 7, f = 2;
            var s = new HashSet<int>();
            while (s.Count < f) s.Add(_rnd.Next(n));

            SetUpSomeSilent(n, f, s);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n - f, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        public void TestOneMalicious()
        {
            const int n = 7, f = 2;
 
            SetUpOneMalicious(n, f);
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 1; i < n; ++i)
            {
                _broadcasts[i].WaitFinish();
            }

            for (var i = 1; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n, _resultInterceptors[i].Result.Count);
            }
        }

        [Test]
        //[Ignore("don't work with invalid shares")]
        public void TestOneMaliciousWithTx()
        {
            const int n = 7, f = 2;
 
            SetUpOneMalicious(n, f);

            for (var i = 0 ; i < n ; i++)
            {
                Logger.LogInformation($"My validator id {_broadcasters[i].GetMyId()}");
                Assert.AreEqual(i, _broadcasters[i].GetMyId());
            }

            var inputs = new List<List<TransactionReceipt>>();
            for (var i = 0; i < n; i++)
            {
                var randomValue = GetRandomTxes();
                inputs.Add(randomValue);
            }
            // inputs = inputs.OrderBy(tx => tx, new ReceiptComparer()).ToList();
            for (var i = 0; i < n ; i++)
            {
                var share = new RawShare(inputs[i].ToByteArray(), i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                while (!_broadcasts[i].Terminated);
            }
            List<TransactionReceipt>[] txes = new List<TransactionReceipt>[n];
            for (int i = 0 ; i < n ; i++)
            {
                var rawShares = _resultInterceptors[i].GetResult();
                txes[i] = new List<TransactionReceipt>();
                // if (i == 0 && rawShares is null) continue;
                Logger.LogInformation($"Got result for {_resultInterceptors[i].Id}");
                foreach (var share in rawShares)
                {
                    try 
                    {
                        var contributions = share.ToBytes().ToMessageArray<TransactionReceipt>();
                        foreach(var receipt in contributions)
                            txes[i].Add(receipt);
                    }
                    catch(Exception e)
                    {
                        Logger.LogError($"Skipped a rawShare due to exception: {e.Message}");
                        Logger.LogError($"One of the validators might be malicious!!!");
                    }
                }
                txes[i] = txes[i].Distinct().ToArray().ToList();
                txes[i] = txes[i].OrderBy(tx => tx, new ReceiptComparer()).ToList();
            }

            var inputTxes = new List<TransactionReceipt>();
            foreach (var list in inputs)
            {
                inputTxes.AddRange(list);
            }
            inputTxes = inputTxes.Distinct().OrderBy(tx => tx, new ReceiptComparer()).ToList();
            Logger.LogInformation($"input for malicious nodes: {inputs[0].Count}");
            foreach (var tx in inputs[0])
            {
                Logger.LogInformation(tx.Hash.ToHex());
            }
            Logger.LogInformation($"inputs: tx count: {inputTxes.Count}");
            foreach (var tx in inputTxes)
            {
                Logger.LogInformation(tx.Hash.ToHex());
            }

            for (int i = 0; i < n ; i++)
            {
                Logger.LogInformation($"Got result for {_resultInterceptors[i].Id}: tx count: {txes[i].Count}");
                foreach (var tx in txes[i])
                {
                    Logger.LogInformation(tx.Hash.ToHex());
                }
            }

            // all honest nodes have same tx list
            for (int i = 2; i < n ; i++)
            {
                Assert.AreEqual(txes[i], txes[i-1]);
            }

            int count = 0;
            var notFound = new List<TransactionReceipt>();
            for (var iter = 0 ; iter < inputTxes.Count; iter++)
            {
                bool found = false;
                foreach (var tx in txes[1])
                {
                    if (tx.Hash.Equals(inputTxes[iter].Hash))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    notFound.Add(inputTxes[iter]);
                    count++;
                    Logger.LogInformation($"Could not find tx {iter} with hash {inputTxes[iter].Hash.ToHex()} "
                        + $"transaction: {inputTxes[iter].ToString()}");
                }
            }
            Logger.LogInformation($"Could not found total {count} txes");
            foreach (var tx in notFound)
            {
                Logger.LogInformation(tx.Hash.ToHex() + " belongs to");
                for (int iter = 0 ; iter < n ; iter++)
                {
                    if (inputs[iter].Contains(tx))
                    {
                        Logger.LogInformation($"{iter}");
                    }
                }
            }
            // no tx should be missing
            Assert.AreEqual(0, count, "tx missing");

            for (var i = 1; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n, _resultInterceptors[i].Result.Count);
            }
            Stop(n);
        }

        [Test]
        //[Ignore("don't work with invalid shares")]
        public void TestOneSmartMaliciousWithTx()
        {
            const int n = 7, f = 2;
 
            SetUpOneSmartMalicious(n, f);

            for (var i = 0 ; i < n ; i++)
            {
                Logger.LogInformation($"My validator id {_broadcasters[i].GetMyId()}");
                Assert.AreEqual(i, _broadcasters[i].GetMyId());
            }

            var inputs = new List<List<TransactionReceipt>>();
            for (var i = 0; i < n; i++)
            {
                var randomValue = GetRandomTxes();
                inputs.Add(randomValue);
            }
            // inputs = inputs.OrderBy(tx => tx, new ReceiptComparer()).ToList();
            for (var i = 0; i < n ; i++)
            {
                var share = new RawShare(inputs[i].ToByteArray(), i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                while (!_broadcasts[i].Terminated);
            }
            List<TransactionReceipt>[] txes = new List<TransactionReceipt>[n];
            for (int i = 0 ; i < n ; i++)
            {
                var rawShares = _resultInterceptors[i].GetResult();
                txes[i] = new List<TransactionReceipt>();
                // if (i == 0 && rawShares is null) continue;
                Logger.LogInformation($"Got result for {_resultInterceptors[i].Id}");
                foreach (var share in rawShares)
                {
                    try 
                    {
                        var contributions = share.ToBytes().ToMessageArray<TransactionReceipt>();
                        foreach(var receipt in contributions)
                            txes[i].Add(receipt);
                    }
                    catch(Exception e)
                    {
                        Logger.LogError($"Skipped a rawShare due to exception: {e.Message}");
                        Logger.LogError($"One of the validators might be malicious!!!");
                    }
                }
                txes[i] = txes[i].Distinct().ToArray().ToList();
                txes[i] = txes[i].OrderBy(tx => tx, new ReceiptComparer()).ToList();
            }

            var inputTxes = new List<TransactionReceipt>();
            foreach (var list in inputs)
            {
                inputTxes.AddRange(list);
            }
            inputTxes = inputTxes.Distinct().OrderBy(tx => tx, new ReceiptComparer()).ToList();
            Logger.LogInformation($"input for malicious nodes: {inputs[0].Count}");
            foreach (var tx in inputs[0])
            {
                Logger.LogInformation(tx.Hash.ToHex());
            }
            Logger.LogInformation($"inputs: tx count: {inputTxes.Count}");
            foreach (var tx in inputTxes)
            {
                Logger.LogInformation(tx.Hash.ToHex());
            }

            for (int i = 0; i < n ; i++)
            {
                Logger.LogInformation($"Got result for {_resultInterceptors[i].Id}: tx count: {txes[i].Count}");
                foreach (var tx in txes[i])
                {
                    Logger.LogInformation(tx.Hash.ToHex());
                }
            }

            // all honest nodes have same tx list
            for (int i = 2; i < n ; i++)
            {
                Assert.AreEqual(txes[i], txes[i-1]);
            }

            int count = 0;
            var notFound = new List<TransactionReceipt>();
            for (var iter = 0 ; iter < inputTxes.Count; iter++)
            {
                bool found = false;
                foreach (var tx in txes[1])
                {
                    if (tx.Hash.Equals(inputTxes[iter].Hash))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    notFound.Add(inputTxes[iter]);
                    count++;
                    Logger.LogInformation($"Could not find tx {iter} with hash {inputTxes[iter].Hash.ToHex()} "
                        + $"transaction: {inputTxes[iter].ToString()}");
                    // Assert.That(iter == n-1);
                }
            }
            Logger.LogInformation($"Could not found total {count} txes");
            foreach (var tx in notFound)
            {
                Logger.LogInformation(tx.Hash.ToHex() + " belongs to");
                for (int iter = 0 ; iter < n ; iter++)
                {
                    if (inputs[iter].Contains(tx))
                    {
                        Logger.LogInformation($"{iter}");
                    }
                }
            }
            // no tx should be missing
            Assert.AreEqual(0, count, "tx missing");

            for (var i = 1; i < n; ++i)
            {
                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n, _resultInterceptors[i].Result.Count);
            }
            Stop(n);
        }

        [Test]
        [Repeat(5)]
        public void RandomTest()
        {
            var n = _rnd.Next(1, 10);
            var f = _rnd.Next((n - 1) / 3 + 1);
            var mode = _rnd.SelectRandom(Enum.GetValues(typeof(DeliveryServiceMode)).Cast<DeliveryServiceMode>());
            var s = new HashSet<int>();
            while (s.Count < f) s.Add(_rnd.Next(n));

            SetUpSomeSilent(n, f, s);
            _deliveryService.Mode = mode;
            for (var i = 0; i < n; ++i)
            {
                var share = new RawShare(new byte[32], i);
                _broadcasters[i].InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                    _resultInterceptors[i].Id, (_broadcasts[i].Id as HoneyBadgerId)!, share
                ));
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;
                _broadcasts[i].WaitFinish();
            }

            for (var i = 0; i < n; ++i)
            {
                if (s.Contains(i)) continue;

                Assert.IsTrue(_broadcasts[i].Terminated, $"protocol {i} did not terminated");
                Assert.AreEqual(_resultInterceptors[i].ResultSet, 1,
                    $"protocol {i} has emitted result not once but {_resultInterceptors[i].ResultSet}");
                Assert.AreEqual(n - f, _resultInterceptors[i].Result.Count);
            }
        }

        private List<TransactionReceipt> GetRandomTxes()
        {
            var txes = new List<TransactionReceipt>();
            int count = _rnd.Next(1,11);
            for (int i = 0 ; i < count ; i++)
            {
                txes.Add(TestUtility.GetRandomTransaction(false));
            }
            return txes.OrderBy(tx => tx, new ReceiptComparer()).ToList();
        }
    }
}