using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Logger;
using Lachain.Storage;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Proto;
using System.Collections.Generic;
using System.Linq;


namespace Lachain.StorageTest
{
    public class RocksDbTest
    {
        private static readonly ILogger<RocksDbTest> Logger = LoggerFactory.GetLoggerForClass<RocksDbTest>();
        private IContainer _container;
        private IRocksDbContext _dbContext;
        private readonly int _dbUpdatePeriod = 100000;
        private int _counter;
        private RocksDbAtomicWrite _batchWrite;

        public RocksDbTest()
        {
        }

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _dbContext = _container.Resolve<IRocksDbContext>();
        }

        [TearDown]
        public void TearDown()
        {
            _container.Dispose();;
            TestUtils.DeleteTestChainData();
        }

        [Test]
        [Repeat(1)]
        public void Test_SeekAndSerialized()
        {
            
            var bytes = new byte[2];
            var values = new List<byte[]>();
            int test = 10;
            var keyValues = new List<(byte[], byte[])>();
            for (int i = 0 ; i < test; i++)
            {
                for (int j = 0 ; j < test; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    var value = new byte[1]{10};
                    values.Add(value);
                    keyValues.Add((bytes, value));
                    Save(bytes, value);
                }
            }
            Commit();
            
            var startTime = TimeUtils.CurrentTimeMillis();
            var iterator = _dbContext.GetIteratorForValidKeys(new byte[1]{0});
            int iter = 0;
            for (int i = 0 ; i < test; i++)
            {
                for (int j = 0 ; j < test; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    Assert.That(iterator.Valid());
                    var key = iterator.Key();
                    var value = iterator.Value();
                    iterator = iterator.Next();
                    Assert.AreEqual(bytes, key);
                    Assert.AreEqual(values[iter], value);
                    iter++;
                }
            }
            Assert.That(!iterator.Valid());

            CheckDb(keyValues);
        }

        [Test]
        public void Test_SeekAndSerializedRandomValue()
        {
            int test = 10;
            var values = new List<(ulong, byte[])>();
            var keys = new List<ulong>();
            var prefix = new byte[2];
            prefix[0] = 2;
            prefix[1] = 3;
            for (int iter = 0 ; iter < test; iter++)
            {
                var bytes = TestUtils.GetRandomValue(8);
                var num = BitConverter.ToUInt64(bytes);
                Assert.AreEqual(bytes, UInt64Utils.ToBytes(num));
                keys.Add(num);
                var key = BuildPrefix(prefix, num);
                var value = TestUtils.GetRandomValue();
                if (values.Count > 0)
                {
                    while (value.SequenceEqual(values.Last().Item2))
                        value = TestUtils.GetRandomValue(1);
                }
                Save(key, value);
                values.Add((num,value));
            }
            Commit();
            for (int i = 1 ; i < values.Count; i++)
            {
                Assert.AreNotEqual(values[i].Item2, values[i-1].Item2);
            }

            keys = keys.OrderBy(x => x, new UlongKeyCompare()).ToList();
            values = values.OrderBy(value => value.Item1, new UlongKeyCompare()).ToList();
            
            var iterator = _dbContext.GetIteratorForValidKeys(prefix);
            for (int iter = 0 ; iter < test; iter++)
            {
                Assert.That(iterator.Valid());
                var bytes = iterator.Key();
                Assert.AreEqual(prefix, bytes.Take(2).ToArray());
                var key = BitConverter.ToUInt64(bytes.Skip(2).ToArray());
                var value = iterator.Value().ToArray();
                Assert.AreEqual(values[iter].Item2, value);
                Assert.AreEqual(values[iter].Item1, key);
                Assert.AreEqual(values[iter].Item1, keys[iter]);
                iterator.Next();
            }

            Assert.That(!iterator.Valid());

            var keyValues = new List<(byte[], byte[])>();
            foreach (var (key, value) in values)
            {
                keyValues.Add((prefix.Concat(UInt64Utils.ToBytes(key)).ToArray(), value));
            }
            CheckDb(keyValues);
        }

        [Test]
        public void Test_SeekSerializedWithSamePrefix()
        {
            int noOfPrefix = 10; // don't use too much, prefixes should not be same for test to work properly
            int noOfTest = 10;
            InsertRandomPrefixKeyValue(noOfPrefix, noOfTest, out var prefixKeyValues, out var keyValues);
            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyCompare()).ToList();
            Assert.AreEqual(noOfPrefix * noOfTest, prefixKeyValues.Count);
            for (int i = 0 ; i < noOfPrefix; i++)
            {
                var prefix = prefixKeyValues[0].Item1;
                Logger.LogInformation($"prefix: {prefix[0]}, {prefix[1]}");
                var items = prefixKeyValues.Take(noOfTest).Select(item => item.Item2).OrderBy(
                    item => item.Item1, new UlongKeyCompare()).ToArray();
                Assert.AreEqual(items.Length, noOfTest);

                var lastPrefix = !GetNextValue(prefix, out var upperBound);

                RocksDbSharp.Iterator iterator;
                if (lastPrefix) iterator = _dbContext.GetIteratorForValidKeys(prefix);
                else iterator = _dbContext.GetIteratorWithUpperBound(prefix, upperBound);
                for (int iter = 0; iter < noOfTest; iter++)
                {
                    Assert.That(iterator.Valid());
                    var key = iterator.Key();
                    var value = iterator.Value();
                    Assert.AreEqual(prefix, key.Take(2).ToArray());
                    Assert.AreEqual(items[iter].Item1, BitConverter.ToUInt64(key.Skip(2).ToArray()));
                    Assert.AreEqual(items[iter].Item2, value);
                    iterator.Next();
                }
                Assert.That(!iterator.Valid());
                prefixKeyValues = prefixKeyValues.Skip(noOfTest).ToList();
            }
            Assert.AreEqual(0, prefixKeyValues.Count);

            CheckDb(keyValues);
        }

        [Test]
        public void Test_SeekAndSerializedWithMultiplePrefix()
        {
            int noOfPrefix = 10; // don't use too much, prefixes should not be same for test to work properly
            int noOfTest = 10;
            InsertRandomPrefixKeyValue(noOfPrefix, noOfTest, out var prefixKeyValues, out var keyValues);
            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyCompare()).ToList();
            Assert.AreEqual(noOfPrefix * noOfTest, prefixKeyValues.Count);
            var sortedKeyValues = new List<(byte[], (ulong, byte[]))>();
            for (int i = 0 ; i < noOfPrefix; i++)
            {
                var currentKeyValues = prefixKeyValues.Take(noOfTest).OrderBy(
                    item => item.Item2.Item1, new UlongKeyCompare()).ToList();
                sortedKeyValues.AddRange(currentKeyValues);
                prefixKeyValues = prefixKeyValues.Skip(noOfTest).ToList();
            }
            Assert.AreEqual(0, prefixKeyValues.Count);
            Assert.AreEqual(noOfPrefix * noOfTest, sortedKeyValues.Count);

            var iterator = _dbContext.GetIteratorForValidKeys(Array.Empty<byte>());
            foreach (var (prefix, (num, value)) in sortedKeyValues)
            {
                Assert.That(iterator.Valid());
                var key = iterator.Key();
                Assert.AreEqual(prefix, key.Take(2).ToArray());
                Assert.AreEqual(num, BitConverter.ToUInt64(key.Skip(2).ToArray()));
                Assert.AreEqual(value, iterator.Value());
                iterator.Next();
            }
            Assert.That(!iterator.Valid());

            iterator = _dbContext.GetIteratorForValidKeys(sortedKeyValues[0].Item1);
            foreach (var (prefix, (num, value)) in sortedKeyValues)
            {
                Assert.That(iterator.Valid());
                var key = iterator.Key();
                Assert.AreEqual(prefix, key.Take(2).ToArray());
                Assert.AreEqual(num, BitConverter.ToUInt64(key.Skip(2).ToArray()));
                Assert.AreEqual(value, iterator.Value());
                iterator.Next();
            }
            Assert.That(!iterator.Valid());

            CheckDb(keyValues);
        }

        [Test]
        public void Test_SeekAndUpperBound()
        {
            int noOfTest = 10;
            int noOfPrefix = 5; // don't use more than 256, don't use too much, slow downs the unit test
            var keyValues = new List<(byte[], byte[])>();
            var prefixKeyValues = new List<(byte[], (ulong, byte[]))>();
            for (int i = 0 ; i < noOfPrefix; i++)
            {
                for (int j = 0 ; j < noOfPrefix; j++)
                {
                    var prefix = new byte[2]{(byte) i, (byte) j};
                    if (i == noOfPrefix - 1) prefix[0] = 255;
                    if (j == noOfPrefix - 1) prefix[1] = 255;
                    for (int iter = 0 ; iter < noOfTest; iter++)
                    {
                        var bytes = TestUtils.GetRandomValue(8);
                        var value = TestUtils.GetRandomValue();
                        keyValues.Add((BuildPrefix(prefix, bytes), value));
                        prefixKeyValues.Add((prefix, (BitConverter.ToUInt64(bytes), value)));
                    }
                }
            }

            var rnd = new Random();
            keyValues = keyValues.OrderBy(_ => rnd.Next()).ToList();

            foreach (var (key, value) in keyValues)
            {
                _dbContext.Save(key, value);
            }

            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyCompare()).ToList();
            Assert.AreEqual(noOfPrefix * noOfPrefix * noOfTest, prefixKeyValues.Count);

            for (int i = 0 ; i < noOfPrefix * noOfPrefix; i++)
            {
                var prefix = prefixKeyValues[0].Item1;
                var items = prefixKeyValues.Take(noOfTest).Select(
                    item => item.Item2).OrderBy(item => item.Item1, new UlongKeyCompare()).ToArray();
                var lastPrefix = !GetNextValue(prefix, out var upperBound);
                RocksDbSharp.Iterator iterator;
                if (lastPrefix) iterator = _dbContext.GetIteratorForValidKeys(prefix);
                else iterator = _dbContext.GetIteratorWithUpperBound(prefix, upperBound);

                Assert.AreEqual(noOfTest, items.Length);

                foreach (var (num, value) in items)
                {
                    Assert.That(iterator.Valid());
                    var key = iterator.Key();
                    Assert.AreEqual(prefix, key.Take(2).ToArray());
                    Assert.AreEqual(num, BitConverter.ToUInt64(key.Skip(2).ToArray()));
                    Assert.AreEqual(value, iterator.Value());
                    iterator.Next();
                }
                Assert.That(!iterator.Valid());
                prefixKeyValues = prefixKeyValues.Skip(noOfTest).ToList();
            }

            Assert.AreEqual(0, prefixKeyValues.Count);

            CheckDb(keyValues);
        }

        [Test]
        public void Test_IterationSpeed()
        {
            int noOfPrefix = 1; // don't use too much, prefixes should not be same for test to work properly
            int noOfTest = 10;
            InsertRandomPrefixKeyValue(noOfPrefix, noOfTest, out var prefixKeyValues, out var keyValues);
            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyCompare()).ToList();
            Assert.AreEqual(noOfPrefix * noOfTest, prefixKeyValues.Count);
            keyValues = keyValues.OrderBy(item => item.Item1, new ByteKeyCompare()).ToList();
            Assert.AreEqual(noOfPrefix * noOfTest, keyValues.Count);

            var sortedKeyValues = new List<(byte[], (ulong, byte[]))>();
            for (int i = 0 ; i < noOfPrefix; i++)
            {
                var items = prefixKeyValues.Take(noOfTest).OrderBy(
                    item => item.Item2.Item1, new UlongKeyCompare()).ToList();
                sortedKeyValues.AddRange(items);
                prefixKeyValues = prefixKeyValues.Skip(noOfTest).ToList();
            }
            Assert.AreEqual(0, prefixKeyValues.Count);
            Assert.AreEqual(noOfPrefix * noOfTest, sortedKeyValues.Count);

            Logger.LogInformation($"getting {noOfPrefix * noOfTest} (key,value) pairs via iteration");
            var startTime = TimeUtils.CurrentTimeMillis();
            var iterator = _dbContext.GetIteratorForValidKeys(sortedKeyValues[0].Item1);
            // var savedPair = new List<(byte[], byte[])>();
            for (int iter = 0; iter < sortedKeyValues.Count; iter++)
            {
                Assert.That(iterator.Valid());
                var key = iterator.Key();
                var value = iterator.Value();
                // savedPair.Add((key, value));
                iterator.Next();
            }
            Assert.That(!iterator.Valid());
            Logger.LogInformation($"Time taken via iteration {TimeUtils.CurrentTimeMillis() - startTime} ms");

            // startTime = TimeUtils.CurrentTimeMillis();
            // for (int i = 0; i < keyValues.Count; i++)
            // {
            //     Assert.AreEqual(keyValues[i].Item1, savedPair[i].Item1);
            //     Assert.AreEqual(keyValues[i].Item2, savedPair[i].Item2);
            // }
            // Logger.LogInformation($"time taken to verify data via iteration "
            //     + $"{TimeUtils.CurrentTimeMillis() - startTime} ms");

            Logger.LogInformation($"getting {noOfPrefix * noOfTest} (key,value) pairs via Get method");
            startTime = TimeUtils.CurrentTimeMillis();
            // savedPair = new List<(byte[], byte[])>();
            foreach (var (prefix, (num, _)) in sortedKeyValues)
            {
                var key = BuildPrefix(prefix, num);
                var value = _dbContext.Get(key);
                // savedPair.Add((key, value));
            }
            Logger.LogInformation($"Time taken via Get method {TimeUtils.CurrentTimeMillis() - startTime} ms");
            
            // startTime = TimeUtils.CurrentTimeMillis();
            // for (int i = 0; i < keyValues.Count; i++)
            // {
            //     Assert.AreEqual(keyValues[i].Item1, savedPair[i].Item1);
            //     Assert.AreEqual(keyValues[i].Item2, savedPair[i].Item2);
            // }
            // Logger.LogInformation($"time taken to verify data via Get method "
            //     + $"{TimeUtils.CurrentTimeMillis() - startTime} ms");
            
            // CheckDb(keyValues);
        }

        private bool GetNextValue(byte[] prefix, out byte[] nextPrefix)
        {
            nextPrefix = new List<byte>(prefix).ToArray();
            if (nextPrefix[1] < 255) nextPrefix[1]++;
            else if (nextPrefix[0] < 255)
            {
                nextPrefix[0]++;
                nextPrefix[1] = 0;
            }
            else
            {
                return false;
            }
            return true;
        }

        private void InsertRandomPrefixKeyValue(
            int noOfPrefix, 
            int noOfTest, 
            out List<(byte[], (ulong, byte[]))> prefixKeyValues,
            out List<(byte[], byte[])> keyValues
        )
        {
            Initialize();
            prefixKeyValues = new List<(byte[], (ulong, byte[]))>();
            keyValues = new List<(byte[], byte[])>();
            var prevPrefix = new byte[0];
            for (int i = 0 ; i < noOfPrefix; i++)
            {
                var prefix = TestUtils.GetRandomValue(2);
                while (prefix.SequenceEqual(prevPrefix))
                    prefix = TestUtils.GetRandomValue(2);
                prevPrefix = prefix;
                for (int iter = 0; iter < noOfTest; iter++)
                {
                    var bytes = TestUtils.GetRandomValue(8);
                    var value = TestUtils.GetRandomValue();
                    var num = BitConverter.ToUInt64(bytes);
                    prefixKeyValues.Add((prefix, (num, value)));
                    var key = BuildPrefix(prefix, num);
                    keyValues.Add((key, value));
                }
            }

            var rnd = new Random();
            keyValues = keyValues.OrderBy(_ => rnd.Next()).ToList();
            var startTime = TimeUtils.CurrentTimeMillis();
            foreach (var (key, value) in keyValues)
            {
                Save(key, value);
            }
            Commit();
            Logger.LogInformation($"time taken to insert {noOfPrefix * noOfTest} (key, value) "
                + $"in db {TimeUtils.CurrentTimeMillis() - startTime} ms");
        }

        private void Save(byte[] key, byte[] value)
        {
            _batchWrite.Put(key, value);
            UpdateCounter();
            if (CycleEnded()) Commit();
        }

        private void Delete(byte[] key)
        {
            _batchWrite.Delete(key);
            UpdateCounter();
            if (CycleEnded()) Commit();
        }

        private void CheckDb(List<(byte[], byte[])> keyValues)
        {
            foreach (var (key, value) in keyValues)
            {
                var gotValue = _dbContext.Get(key);
                Assert.AreEqual(value, gotValue);
            }
        }

        private byte[] BuildPrefix(byte[] prefix, ulong key)
        {
            return BuildPrefix(prefix, UInt64Utils.ToBytes(key));
        }

        private byte[] BuildPrefix(byte[] prefix, byte[] key)
        {
            var res = new List<byte>();
            foreach (var element in prefix)
                res.Add(element);

            foreach (var element in key)
                res.Add(element);
            
            return res.ToArray();
        }

        private void Print(byte[] bytes, string startMsg, string endMsg)
        {
            Logger.LogInformation(startMsg);
            Logger.LogInformation($"Length: {bytes.Length}");
            foreach (var ele in bytes)
            {
                Logger.LogInformation($"{ele}");
            }
            Logger.LogInformation(endMsg);
        }

        private void UpdateCounter()
        {
            _counter--;
        }

        private void ResetCounter()
        {
            _counter = _dbUpdatePeriod;
        }

        private bool CycleEnded()
        {
            return _counter <= 0;
        }

        private void Commit()
        {
            _batchWrite.Commit();
            Initialize();
        }

        private void Initialize()
        {
            ResetCounter();
            _batchWrite = new RocksDbAtomicWrite(_dbContext);
        }
    }

    public class UlongKeyCompare : IComparer<ulong>
    {
        public int Compare(ulong x, ulong y)
        {
            if (x == y) return 0;
            for (int i = 0 ; i < 8; i++)
            {
                var xChunk = (x >> (8*i)) & 0xFF;
                var yChunk = (y >> (8*i)) & 0xFF;
                if (xChunk != yChunk) return xChunk.CompareTo(yChunk);
            }
            return 0;
        }
    }

    public class ByteKeyCompare : IComparer<byte[]>
    {
        public int Compare(byte[] x, byte[] y)
        {
            for (int iter = 0 ; iter < x.Length; iter++)
            {
                if (iter >= y.Length) return 1;
                if (x[iter] != y[iter]) return x[iter].CompareTo(y[iter]);
            }
            return x.Length == y.Length ? 0 : -1;
        }
    }
    
}