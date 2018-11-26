using System;
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
            var mapStorageContext = new PersistentMapStorageContext<UInt256, Block>(blockRepo, new BlockMapFactory(0));
            var blockManager = new BlockMapManager(mapStorageContext, new UInt256Comparer());
            
            
            var root = mapStorageContext.NullIDentifier;
            Random random = new Random();
            var start = TimeUtils.CurrentTimeMillis();
            ulong T = 100000;
            for (var i = 0u; i < T; ++i)
            {
                var key = new BigInteger(random.Next()).ToUInt256();
                var value = new Block();
                root = blockManager.Add(root, key, value);
            }

            var finish = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{T} insertions in {finish - start}ms");
            Console.WriteLine($"{(double) (finish - start) / T}ms per insertion");
            Console.WriteLine($"{(double) T * 1000 / (finish - start)} insertions per sec");
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