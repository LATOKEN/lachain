using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Phorkus.Core;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.RocksDB;
using Phorkus.Storage.Mappings;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.Treap;

namespace Phorkus.StorageBenchmark
{
    public class Application : IBootstrapper
    {
        private readonly IContainer _container;

        public Application()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, exception) =>
            {
                Console.Error.WriteLine(exception);
            };

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager("config.json"));

            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        public void Start(string[] args)
        {
            var rocksDbContext = _container.Resolve<IRocksDbContext>();
            var blockRepo = new BlockRepository(rocksDbContext);
            var blockMapFactory = new BlockMapFactory(0);
            var mapStorageContext = new PersistentMapStorageContext<UInt256, Block>(blockRepo);
            var blockManager = new BlockMapManager(mapStorageContext, new UInt256Comparer(), blockMapFactory);


            var root = blockMapFactory.NullIdentifier;
            Random random = new Random();
            var start = TimeUtils.CurrentTimeMillis();
            ulong T = 300001;
            IDictionary<UInt256, Block> blocks = new Dictionary<UInt256, Block>();

//            Full persistence, old version
//            990000 insertions in 421546ms
//            0.42580404040404ms per insertion
//            2348.49814729591 insertions per sec
            
//            Full persistence, new version
//            1000000 insertions in 165128ms
//            0.165128ms per insertion
//            6055.90814398527 insertions per sec
            
//            In memory, ToHex comparer
//            1000000 insertions in 228409ms
//            0.228409ms per insertion
//            4378.11119526814 insertions per sec
            
//            In memory, memcmp from libc compare with ToByteArray
//            1000000 insertions in 27670ms
//            0.02767ms per insertion
//            36140.2240693892 insertions per sec
            
//            Somewhere here: memcpy with reflection for private field: ~0.02ms per insertion

//            In memory, for loop comparer
//            1000000 insertions in 13787ms
//            0.013787ms per insertion
//            72532.0954522376 insertions per sec

            for (var i = 0u; i < T; ++i)
            {
                if (blocks.Count == 0 || random.Next() % 3 != 0)
                {
                    var key = new BigInteger(random.Next()).ToUInt256();
                    var value = new Block();
                    blocks[key] = value;
                    root = blockManager.Add(root, key, value);
                }
                else
                {
                    var key = blocks.Keys.First();
                    blocks.Remove(key);
                    root = blockManager.TryDelete(root, key, out var block);
                }

//                var actualKeys = blockManager.GetKeys(root).ToList();
//                if (!blocks.Keys.OrderBy(x => x.Buffer.ToHex()).SequenceEqual(actualKeys))
//                {
//                    Console.WriteLine("FOOO");
//                }

                if (i > 0 && i % 10000 == 0)
                {
                    var finish = TimeUtils.CurrentTimeMillis();
                    Console.WriteLine($"{i} insertions in {finish - start}ms");
                    Console.WriteLine($"{(double) (finish - start) / i}ms per insertion");
                    Console.WriteLine($"{(double) i * 1000 / (finish - start)} insertions per sec");
                }
            }

            
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var app = new Application();
            app.Start(args);
        }
    }
}