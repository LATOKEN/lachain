using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Crypto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Proto;
using System.Collections.Generic;
using System.Linq;


namespace Lachain.StorageTest
{
    public class StorageTest
    {
        private IContainer _container;
        private IStateManager _stateManager;

        private const uint T = 10;
        private const uint batches = 100;
        private UInt160 contract = UInt160Utils.Zero;

        public StorageTest()
        {
        }
        private static uint _next = 48821;

        private static uint Rand()
        {
            unchecked
            {
                _next = _next * 1103515245 + 12345;
                return _next / 2;
            }
        }

        private static UInt256 RandUInt256()
        {
            var buffer = new byte[32];
            for (var i = 0; i < 8; ++i)
            {
                var x = Rand();
                for (var j = 0; j < 4; ++j)
                    buffer[i * 4 + j] = (byte)((x >> (8 * j)) & 0xFF);
            }

            return buffer.ToUInt256();
        }

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _stateManager = _container.Resolve<IStateManager>();
        }

        [TearDown]
        public void Teardown()
        {
            _container.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test()
        {
            var crypto = CryptoProvider.GetCrypto();
            int total = 1000_000;
            int commitSize = 100000;
            var startTime = TimeUtils.CurrentTimeMillis();
            for (int iter = 0; iter < total / commitSize; iter++)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (int tx = 0; tx < commitSize; tx++)
                {
                    var receipt = TestUtils.GetRandomTransaction(true);
                    var block = new Block
                    {
                        Header = new BlockHeader
                        {
                            Index = (ulong) iter * (ulong) commitSize + (ulong) tx
                        },
                        Hash = TestUtils.GetRandomBytes(32).ToUInt256(),
                        TransactionHashes = { new UInt256[] { receipt.Hash } }
                    };
                    snapshot.Blocks.AddBlock(block);
                    snapshot.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                }
                _stateManager.Approve();
                _stateManager.Commit();
            }
            System.Console.WriteLine(TimeUtils.CurrentTimeMillis() - startTime);
            ulong totalTime = 0;
            var totalTest = 100000;
            ulong minTime = 1000000000000;
            ulong maxTime = 0;
            for (int iter = 0; iter < totalTest; iter++)
            {
                var randomUInt = crypto.GenerateRandomBytes(4).AsReadOnlySpan().ToUInt32() % total;
                var now = TimeUtils.CurrentTimeMillis();
                var block = _stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight((ulong) randomUInt);
                var receipt = _stateManager.LastApprovedSnapshot.Transactions.GetTransactionByHash(block!.TransactionHashes[0]);
                var timeSpent = TimeUtils.CurrentTimeMillis() - now;
                minTime = Math.Min(minTime, timeSpent);
                maxTime = Math.Max(maxTime, timeSpent);
                // System.Console.WriteLine(timeSpent);
                totalTime += timeSpent;
            }
            System.Console.WriteLine("average to read: " + 1.0 * totalTime / totalTest);
            System.Console.WriteLine("max time: " + maxTime);
            System.Console.WriteLine("min time: " + minTime);

            totalTime = 0;
            minTime = 1000000000000;
            maxTime = 0;
            var snapshotToWrite = _stateManager.NewSnapshot();
            for (int iter = 0; iter < totalTest; iter++)
            {
                var now = TimeUtils.CurrentTimeMillis();
                var receipt = TestUtils.GetRandomTransaction(true);
                snapshotToWrite.Transactions.AddTransaction(receipt, TransactionStatus.Executed);
                var timeSpent = TimeUtils.CurrentTimeMillis() - now;
                minTime = Math.Min(minTime, timeSpent);
                maxTime = Math.Max(maxTime, timeSpent);
                // System.Console.WriteLine(timeSpent);
                totalTime += timeSpent;
            }
            _stateManager.Approve();
            _stateManager.Commit();
            System.Console.WriteLine("average to write: " + 1.0 * totalTime / totalTest);
            System.Console.WriteLine("max time: " + maxTime);
            System.Console.WriteLine("min time: " + minTime);

        }

        [Test]
        public void Test_AddTryGetRandom()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    if (Rand() % 3 != 0)
                    {
                        var key = RandUInt256();
                        var value = RandUInt256();
                        blocks[key] = value;
                        keyList.Add(key);
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else
                    {

                        var key = (Rand() % 2 == 0 && keyList.Count > 0) ? keyList[(int)(Rand() % keyList.Count)] : RandUInt256();
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        var gotValue = snapshot.Storage.GetValue(contract, key);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }
        }



        [Test]
        public void Test_AddGetRandom()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || Rand() % 3 != 0)
                    {
                        var key = RandUInt256();
                        var value = RandUInt256();
                        blocks[key] = value;
                        keyList.Add(key);
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else
                    {
                        var key = keyList[(int)(Rand() % keyList.Count)];
                        var actualValue = blocks[key];
                        var gotValue = snapshot.Storage.GetValue(contract, key);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }
        }


        [Test]
        public void Test_AddTryGetSmallKeySet()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            const uint K = 21;
            for (var it = 0u; it < K; it++)
            {
                UInt256 key = RandUInt256();
                keyList.Add(key);
            }

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || Rand() % 3 != 0)
                    {
                        var key = keyList[(int)(Rand() % keyList.Count)];
                        var value = RandUInt256();
                        blocks[key] = value;
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else
                    {
                        var key = Rand() % 2 == 0 ? keyList[(int)(Rand() % keyList.Count)] : RandUInt256();
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        var gotValue = snapshot.Storage.GetValue(contract, key);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }
        }

        [Test]
        public void Test_AddDeleteRandom()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || Rand() % 3 != 0)
                    {
                        var key = RandUInt256();
                        var value = RandUInt256();
                        blocks[key] = value;
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else
                    {
                        var key = blocks.Keys.First();
                        var actualValue = blocks[key];
                        blocks.Remove(key);
                        snapshot.Storage.DeleteValue(contract, key, out var wasValue);
                        Assert.IsTrue(actualValue.Equals(wasValue));
                    }
                }
                _stateManager.Approve();
                _stateManager.Commit();
            }

        }


        [Test]
        public void Test_AddTryDeleteRandom()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || Rand() % 3 != 0)
                    {
                        var key = RandUInt256();
                        var value = RandUInt256();
                        blocks[key] = value;
                        keyList.Add(key);
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else
                    {
                        var key = Rand() % 2 == 0 && keyList.Count > 0 ? keyList[(int)(Rand() % keyList.Count)] : RandUInt256();
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        if (blocks.ContainsKey(key)) blocks.Remove(key);
                        snapshot.Storage.DeleteValue(contract, key, out var gotValue);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }
        }

        [Test]
        public void Test_AddGetDeleteSmallKeyset()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            const uint K = 21;
            for (var it = 0u; it < K; it++)
            {
                UInt256 key = RandUInt256();
                keyList.Add(key);
            }

            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                for (var i = 0u; i < T; ++i)
                {
                    var op = Rand() % 3;
                    var key = keyList[(int)(Rand() % keyList.Count)];

                    if (op == 0)
                    {
                        var value = RandUInt256();
                        blocks[key] = value;
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else if (op == 1)
                    {
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        var gotValue = snapshot.Storage.GetValue(contract, key);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                    else
                    {
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        if (blocks.ContainsKey(key)) blocks.Remove(key);
                        snapshot.Storage.DeleteValue(contract, key, out var gotValue);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }

        }

        [Test]
        public void Test_HashConsistency()
        {
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();
            List<UInt256> keyList = new List<UInt256>();

            const uint K = 21;
            for (var it = 0u; it < K; it++)
            {
                UInt256 key = RandUInt256();
                keyList.Add(key);
            }


            for (var it = 0u; it < batches; ++it)
            {
                var snapshot = _stateManager.NewSnapshot();
                Assert.IsTrue(snapshot.Storage.IsTrieNodeHashesOk());

                for (var i = 0u; i < T; ++i)
                {
                    var op = Rand() % 3;
                    var key = keyList[(int)(Rand() % keyList.Count)];

                    if (op == 0)
                    {
                        var value = RandUInt256();
                        blocks[key] = value;
                        snapshot.Storage.SetValue(contract, key, value);
                    }
                    else if (op == 1)
                    {
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        var gotValue = snapshot.Storage.GetValue(contract, key);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                    else
                    {
                        var actualValue = blocks.ContainsKey(key) ? blocks[key] : UInt256Utils.Zero;
                        if (blocks.ContainsKey(key)) blocks.Remove(key);
                        snapshot.Storage.DeleteValue(contract, key, out var gotValue);
                        Assert.IsTrue(actualValue.Equals(gotValue));
                    }
                }

                _stateManager.Approve();
                _stateManager.Commit();
            }

        }

    }
}