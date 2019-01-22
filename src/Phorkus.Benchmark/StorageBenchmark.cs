using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Storage;
using Phorkus.Utility.Utils;

namespace Phorkus.Benchmark
{
    public class StorageBenchmark : IBootstrapper
    {
        private readonly IContainer _container;

        public StorageBenchmark()
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

        private static uint _next = 48821;

        private static uint Rand()
        {
            unchecked
            {
                _next = _next * 1103515245 + 12345;
                return _next / 2;
            }
        }

        public void Start(string[] args)
        {
            var rocksDbContext = _container.Resolve<IRocksDbContext>();
            var storageManager = new StorageManager(rocksDbContext);

            const uint T = 100000;
            const uint batches = 100;
            IDictionary<byte[], byte[]> blocks = new Dictionary<byte[], byte[]>();

            var state = storageManager.GetLastState(1);
            Console.WriteLine("Initial repo version: " + state.CurrentVersion);
            for (var it = 0u; it < batches; ++it)
            {
                Console.WriteLine($"commit number {it}");
                var start = TimeUtils.CurrentTimeMillis();
                for (var i = 0u; i < T; ++i)
                {
                    if (blocks.Count == 0 || Rand() % 3 != 0)
                    {
                        var key = BitConverter.GetBytes(Rand());
                        var value = BitConverter.GetBytes(Rand());
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
}