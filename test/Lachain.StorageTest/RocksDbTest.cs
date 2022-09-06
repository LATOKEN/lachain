using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Logger;
using Lachain.Storage;
using Lachain.Storage.DbCompact;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Proto;
using System.Collections.Generic;
using System.Linq;
using RocksDbSharp;


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
        private object _deletionWorker = new object();
        private DbShrinkStatus dbShrinkStatus;

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
            Initialize();
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
            var iterator = _dbContext.GetIteratorForValidKeys(new byte[1]{0})!;
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
            
            var iterator = _dbContext.GetIteratorForValidKeys(prefix)!;
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
            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyComparer()).ToList();
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
                if (lastPrefix) iterator = _dbContext.GetIteratorForValidKeys(prefix)!;
                else iterator = _dbContext.GetIteratorWithUpperBound(prefix, upperBound)!;
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
            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyComparer()).ToList();
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

            var iterator = _dbContext.GetIteratorForValidKeys(Array.Empty<byte>())!;
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

            iterator = _dbContext.GetIteratorForValidKeys(sortedKeyValues[0].Item1)!;
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

            prefixKeyValues = prefixKeyValues.OrderBy(item => item.Item1, new ByteKeyComparer()).ToList();
            Assert.AreEqual(noOfPrefix * noOfPrefix * noOfTest, prefixKeyValues.Count);

            for (int i = 0 ; i < noOfPrefix * noOfPrefix; i++)
            {
                var prefix = prefixKeyValues[0].Item1;
                var items = prefixKeyValues.Take(noOfTest).Select(
                    item => item.Item2).OrderBy(item => item.Item1, new UlongKeyCompare()).ToArray();
                var lastPrefix = !GetNextValue(prefix, out var upperBound);
                RocksDbSharp.Iterator iterator;
                if (lastPrefix) iterator = _dbContext.GetIteratorForValidKeys(prefix)!;
                else iterator = _dbContext.GetIteratorWithUpperBound(prefix, upperBound)!;

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
            // run locally with high value like 10000000 but don't push
            int noOfTest = 10;
            InsertRandomPrefixKeyValue(noOfPrefix, noOfTest, out var _, out var keyValues, false);
            Assert.AreEqual(noOfPrefix * noOfTest, keyValues.Count);

            int totalData = noOfPrefix * noOfTest;
            Logger.LogInformation($"getting {totalData} (key,value) pairs via iteration");
            var startTime = TimeUtils.CurrentTimeMillis();
            var iterator = _dbContext.GetIteratorForValidKeys(Array.Empty<byte>())!;
            for (int iter = 0; iter < totalData; iter++)
            {
                Assert.That(iterator.Valid());
                var key = iterator.Key();
                var value = iterator.Value();
                iterator.Next();
            }
            Assert.That(!iterator.Valid());
            Logger.LogInformation($"Time taken via iteration {TimeUtils.CurrentTimeMillis() - startTime} ms");

            Logger.LogInformation($"getting {totalData} (key,value) pairs as inserted via Get method");
            startTime = TimeUtils.CurrentTimeMillis();
            foreach (var (key, _) in keyValues)
            {
                var value = _dbContext.Get(key);
            }
            Logger.LogInformation($"Time taken via Get method to get data as inserted "
                + $"{TimeUtils.CurrentTimeMillis() - startTime} ms");

            Logger.LogInformation($"getting {totalData} (key,value) pairs serialized via Get method");
            keyValues = keyValues.OrderBy(item => item.Item1, new ByteKeyComparer()).ToList();
            startTime = TimeUtils.CurrentTimeMillis();
            foreach (var (key, _) in keyValues)
            {
                var value = _dbContext.Get(key);
            }
            Logger.LogInformation($"Time taken via Get method to get data serialized "
                + $"{TimeUtils.CurrentTimeMillis() - startTime} ms");

            Logger.LogInformation($"getting {totalData} (key,value) pairs in random order via Get method");
            var rnd = new Random((int) TimeUtils.CurrentTimeMillis());
            keyValues = keyValues.OrderBy(_ => rnd.Next()).ToList();
            startTime = TimeUtils.CurrentTimeMillis();
            foreach (var (key, _) in keyValues)
            {
                var value = _dbContext.Get(key);
            }
            Logger.LogInformation($"Time taken via Get method to get data in random order "
                + $"{TimeUtils.CurrentTimeMillis() - startTime} ms");
        }

        [Test]
        public void Test_DeleteAndCheckIntegrity()
        {
            var idCount = 1000;
            var pairs = new List<((ulong, UInt256), byte[])>();
            for (int iter = 0; iter < idCount; iter++)
            {
                var id = BitConverter.ToUInt64(TestUtils.GetRandomValue(8));
                var hash = TestUtils.GetRandomValue(32).ToUInt256();
                var value = TestUtils.GetRandomValue();
                pairs.Add(((id, hash), value));
            }

            var startTime = TimeUtils.CurrentTimeMillis();
            foreach (var ((id, hash), value) in pairs)
            {
                var key = EntryPrefix.PersistentHashMap.BuildPrefix(id);
                Save(key, value);
                key = EntryPrefix.VersionByHash.BuildPrefix(hash);
                Save(key, UInt64Utils.ToBytes(id));
            }
            Commit();
            Logger.LogInformation($"time taken to insert {idCount * 2} (key, value) "
                + $"in db {TimeUtils.CurrentTimeMillis() - startTime} ms");

            var rnd = new Random((int) TimeUtils.CurrentTimeMillis());
            int saveCount = 100;
            pairs = pairs.OrderBy(_ => rnd.Next()).ToList();
            startTime = TimeUtils.CurrentTimeMillis();
            for (int iter = 0; iter < saveCount; iter++)
            {
                var (id, hash) = pairs[iter].Item1;
                var key = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
                Save(key, new byte[1]);
                key = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix(hash);
                Save(key, new byte[1]);
            }
            Commit();
            Logger.LogInformation($"time taken to insert {saveCount * 2} (key, value) "
                + $"in db {TimeUtils.CurrentTimeMillis() - startTime} ms");

            dbShrinkStatus = DbShrinkStatus.DeleteOldSnapshot;
            MimmicDbShrink(idCount, saveCount);
            Commit();
            Assert.AreEqual(DbShrinkStatus.CheckConsistency, dbShrinkStatus);

            var values = new List<(byte[], byte[])>();
            for (int iter = 0; iter < saveCount; iter++)
            {
                var ((id, hash), value) = pairs[iter];
                var key = EntryPrefix.PersistentHashMap.BuildPrefix(id);
                values.Add((key, value));
                key = EntryPrefix.VersionByHash.BuildPrefix(hash);
                values.Add((key, UInt64Utils.ToBytes(id)));
            }

            CheckDb(values);
            
            for (int iter = saveCount; iter < idCount; iter++)
            {
                var (id, hash) = pairs[iter].Item1;
                var key = EntryPrefix.PersistentHashMap.BuildPrefix(id);
                var value = _dbContext.Get(key);
                Assert.AreEqual(null, value);
                key = EntryPrefix.VersionByHash.BuildPrefix(hash);
                value = _dbContext.Get(key);
                Assert.AreEqual(null, value);
            }

            foreach (var ((id, hash), _) in pairs)
            {
                var key = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix(id);
                var value = _dbContext.Get(key);
                Assert.AreEqual(null, value);
                key = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix(hash);
                value = _dbContext.Get(key);
                Assert.AreEqual(null, value);
            }
            
        }

        private void MimmicDbShrink(int oldKeys, int tempSaved)
        {
            switch (dbShrinkStatus)
            {
                case DbShrinkStatus.DeleteOldSnapshot:
                    // Logger.LogTrace($"Deleting nodes from DB that are not reachable from last {depth} snapshots");
                    DeleteOldSnapshot(oldKeys - tempSaved);
                    dbShrinkStatus = DbShrinkStatus.DeleteTempNodeInfo;
                    goto case DbShrinkStatus.DeleteTempNodeInfo;

                case DbShrinkStatus.DeleteTempNodeInfo:
                    DeleteRecentSnapshotNodeIdAndHash(tempSaved);
                    dbShrinkStatus = DbShrinkStatus.CheckConsistency;
                    break;
                    
                default:
                    throw new Exception("invalid db-shrink-status");

            }
        }

        private void DeleteOldSnapshot(int expectedDeleteCount)
        {
            Task.Factory.StartNew(() =>
            {
                DeleteNodeById(expectedDeleteCount);
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                DeleteNodeIdByHash(expectedDeleteCount);
            }, TaskCreationOptions.LongRunning);

            lock (_deletionWorker)
            {
                dbShrinkStatus = DbShrinkStatus.AsyncDeletionStarted; 
                Monitor.Wait(_deletionWorker);
            }

            Logger.LogInformation($"deleted {2 * expectedDeleteCount} old keys");
        }

        private void DeleteNodeById(int expectedDeleteCount)
        {
            Logger.LogTrace($"Deleting nodes of old snapshot from DB");
            var prefixToDelete = EntryPrefix.PersistentHashMap.BuildPrefix();
            var prefixToKeep = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix();
            var deleted = DeleteOldKeys(prefixToDelete, prefixToKeep);
            Logger.LogTrace($"Deleted {deleted} nodes of old snapshot from DB in total");
            Assert.AreEqual(expectedDeleteCount, deleted);
            NotifyCaller();
        }

        private void DeleteNodeIdByHash(int expectedDeleteCount)
        {
            Logger.LogTrace($"Deleting nodes of old snapshot from DB");
            var prefixToDelete = EntryPrefix.VersionByHash.BuildPrefix();
            var prefixToKeep = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix();
            var deleted = DeleteOldKeys(prefixToDelete, prefixToKeep);
            Logger.LogTrace($"Deleted {deleted} nodes of old snapshot from DB in total");
            Assert.AreEqual(expectedDeleteCount, deleted);
            NotifyCaller();
        }

        private ulong DeleteOldKeys(byte[] prefixToDelete, byte[] prefixToKeep)
        {
            var ptrToDelete = GetIteratorForPrefixOnly(prefixToDelete);
            var ptrToKeep = GetIteratorForPrefixOnly(prefixToKeep);
            if (ptrToDelete is null || !ptrToDelete.Valid()) return 0;
            if (ptrToKeep is null || !ptrToKeep.Valid())
                throw new Exception("Something went wrong, saved nodeId or nodeHash iterator is null or invalid");
            
            ulong keyDeleted = 0;
            var keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
            var keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
            var comparer = new ByteKeyComparer();

            // pointers fetch keys in sorted order, so we can compare them with a loop
            while (ptrToDelete.Valid() && ptrToKeep.Valid())
            {
                var comparison = comparer.Compare(keyToDelete, keyToKeep);
                if (comparison < 0)
                {
                    keyDeleted++;
                    Delete(ptrToDelete.Key());
                    ptrToDelete.Next();
                    if (ptrToDelete.Valid())
                        keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
                }
                else if (comparison == 0)
                {
                    ptrToDelete.Next();
                    ptrToKeep.Next();
                    if (ptrToDelete.Valid())
                        keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
                    if (ptrToKeep.Valid())
                        keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
                }
                else
                {
                    ptrToKeep.Next();
                    if (ptrToKeep.Valid())
                        keyToKeep = ptrToKeep.Key().Skip(2).ToArray();
                }
            }

            while (ptrToDelete.Valid())
            {
                keyDeleted++;
                Delete(ptrToDelete.Key());
                ptrToDelete.Next();
                if (ptrToDelete.Valid())
                    keyToDelete = ptrToDelete.Key().Skip(2).ToArray();
            }

            return keyDeleted;
        }

        private void DeleteRecentSnapshotNodeIdAndHash(int expectedDeleteCount)
        {
            Task.Factory.StartNew(() =>
            {
                DeleteSavedNodeId(expectedDeleteCount);
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                DeleteSavedNodeHash(expectedDeleteCount);
            }, TaskCreationOptions.LongRunning);

            lock (_deletionWorker)
            {
                dbShrinkStatus = DbShrinkStatus.AsyncDeletionStarted;
                Monitor.Wait(_deletionWorker);
            }

            Logger.LogInformation($"deleted {2*expectedDeleteCount} temp keys");
        }

        private void DeleteSavedNodeId(int expectedDeleteCount)
        {
            Logger.LogTrace($"Deleting saved nodeId");
            ulong nodeIdDeleted = 0;
            var prefix = EntryPrefix.NodeIdForRecentSnapshot.BuildPrefix();
            nodeIdDeleted = DeleteAllForPrefix(prefix);
            Logger.LogTrace($"Deleted {nodeIdDeleted} nodeId in total");
            Assert.AreEqual(expectedDeleteCount, nodeIdDeleted);
            NotifyCaller();
        }

        private void DeleteSavedNodeHash(int expectedDeleteCount)
        {
            Logger.LogTrace($"Deleting saved nodeHash");
            ulong nodeHashDeleted = 0;
            var prefix = EntryPrefix.NodeHashForRecentSnapshot.BuildPrefix();
            nodeHashDeleted = DeleteAllForPrefix(prefix);
            Logger.LogTrace($"Deleted {nodeHashDeleted} nodeHash in total");
            Assert.AreEqual(expectedDeleteCount, nodeHashDeleted);
            NotifyCaller();
        }

        private ulong DeleteAllForPrefix(byte[] prefix)
        {
            ulong keyDeleted = 0;
            var iterator = GetIteratorForPrefixOnly(prefix);
            if (!(iterator is null))
            {
                while (iterator.Valid())
                {
                    keyDeleted++;
                    Delete(iterator.Key());
                    iterator.Next();
                }
            }
            return keyDeleted;
        }

        private void NotifyCaller()
        {
            var checkInterval = 1000; // 1 second
            // DbShrinkStatus.AsyncDeletionStarted means the process is requested
            // there are 2 parallel threads running
            // DbShrinkStatus.DeletionStep1Complete means one of the thread is complete
            // notify caller only if all threads are complete
            while (dbShrinkStatus != DbShrinkStatus.AsyncDeletionStarted 
                && dbShrinkStatus != DbShrinkStatus.DeletionStep1Complete)
            {
                Thread.Sleep(checkInterval);
            }
            lock (_deletionWorker)
            {
                if (dbShrinkStatus == DbShrinkStatus.DeletionStep1Complete)
                {
                    dbShrinkStatus = DbShrinkStatus.DeletionStep2Complete;
                }
                else dbShrinkStatus = DbShrinkStatus.DeletionStep1Complete;
                if (dbShrinkStatus == DbShrinkStatus.DeletionStep2Complete)
                {
                    Monitor.PulseAll(_deletionWorker);
                }
            }
        }

        public Iterator? GetIteratorForPrefixOnly(byte[] prefix)
        {
            bool lastPrefix = !GetNextValue(prefix, out var upperBound);
            if (lastPrefix) return _dbContext.GetIteratorForValidKeys(prefix);
            else return _dbContext.GetIteratorWithUpperBound(prefix, upperBound);
        }

        private bool GetNextValue(byte[] prefix, out byte[] nextPrefix)
        {
            var list = new List<byte>(prefix);
            for (int iter = list.Count - 1; iter >= 0; iter--)
            {
                if (list[iter] < 255)
                {
                    list[iter]++;
                    for (int j = iter + 1; j < list.Count; j++)
                    {
                        list[j] = 0;
                    }
                    nextPrefix = list.ToArray();
                    return true;
                }
            }
            list.Add(0);
            nextPrefix = list.ToArray();
            return false;
        }

        private void InsertRandomPrefixKeyValue(
            int noOfPrefix, 
            int noOfTest, 
            out List<(byte[], (ulong, byte[]))> prefixKeyValues,
            out List<(byte[], byte[])> keyValues,
            bool returnData = true
        )
        {
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
                    if(returnData)
                        prefixKeyValues.Add((prefix, (num, value)));
                    var key = BuildPrefix(prefix, num);
                    keyValues.Add((key, value));
                }
            }

            var rnd = new Random((int) TimeUtils.CurrentTimeMillis());
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
    
}