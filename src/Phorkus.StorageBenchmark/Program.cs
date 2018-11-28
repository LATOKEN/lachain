using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.Utils;
using Phorkus.Hestia;
using Phorkus.RocksDB;

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
            var storageManager = new StorageManager(rocksDbContext, new uint[] {1});
            
            Random random = new Random();
            uint T = 100000, batches = 100;
            IDictionary<byte[], byte[]> blocks = new Dictionary<byte[], byte[]>();

            var state = storageManager.NewState(1);
            Console.WriteLine("Initial repo version: " + state.CurrentVersion);
            for (var it = 0u; it < batches; ++it)
            {
                Console.WriteLine($"commit number {it}");
                var start = TimeUtils.CurrentTimeMillis();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || random.Next() % 3 != 0)
                    {
                        var key = BitConverter.GetBytes(random.Next());
                        var value = BitConverter.GetBytes(random.Next());
                        blocks[key] = value;
                        state.AddOrUpdate(key, value);
                    }
                    else
                    {
                        var key = blocks.Keys.First();
                        state.Delete(key, out _);
                        blocks.Remove(key);
                    }
                }

                var inMemPhase = TimeUtils.CurrentTimeMillis();
                Console.WriteLine($"{T} insertions in {inMemPhase - start}ms");
                Console.WriteLine($"{(double) (inMemPhase - start) / T}ms per insertion");
                Console.WriteLine($"{(double) T * 1000 / (inMemPhase - start)} insertions per sec");

                state.Commit();
                var commit = TimeUtils.CurrentTimeMillis();
            
                Console.WriteLine($"Commited {T} operations in {commit - inMemPhase}ms");
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