using System;
using System.Threading;
using Phorkus.Core;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Utils;
using Phorkus.Proto;
using Phorkus.RocksDB;
using Phorkus.Storage;

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
            var testRepo = new TestRepository<string, string>(rocksDbContext);
            var mapStorage = new PersistentMapStorageContext<string, string>(testRepo);
            var mapManager = new PersistentTreeMapManager<PersistentTreeMap, string, string>(mapStorage);

            var root = mapStorage.NullIDentifier;
            Random random = new Random();
            var start = TimeUtils.CurrentTimeMillis();
            ulong T = 10000;
            for (var i = 0u; i < T; ++i)
            {
                var key = random.Next().ToString();
                var value = random.Next().ToString();
                root = mapManager.Add(root, key, value);
            }

            var finish = TimeUtils.CurrentTimeMillis();
            Console.WriteLine($"{T} insertions in {finish - start}ms");
            Console.WriteLine($"{(double) (finish - start) / T}ms per insertion");
        }

        private bool _interrupt;
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