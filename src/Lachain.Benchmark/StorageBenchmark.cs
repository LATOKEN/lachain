using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Utils;

namespace Lachain.Benchmark
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

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager("config.json", new RunOptions()));

            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        private static string? ArgGetter(string arg1, string? arg2)
        {
            throw new NotImplementedException();
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
                    buffer[i * 4 + j] = (byte) ((x >> (8 * j)) & 0xFF);
            }

            return buffer.ToUInt256();
        }

        public void Start(RunOptions options)
        {
            var stateManager = _container.Resolve<IStateManager>();
            
            const uint T = 100000;
            const uint batches = 100;
            IDictionary<UInt256, UInt256> blocks = new Dictionary<UInt256, UInt256>();

            var contract = UInt160Utils.Zero;

            Console.WriteLine("Initial repo version: " + stateManager.LastApprovedSnapshot.Storage.Version);
            for (var it = 0u; it < batches; ++it)
            {
                Console.WriteLine($"commit number {it}");
                var snapshot = stateManager.NewSnapshot();
                var start = TimeUtils.CurrentTimeMillis();
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
                        snapshot.Storage.DeleteValue(contract, key, out var wasValue);
                        if (!wasValue.Equals(blocks[key]))
                            throw new InvalidOperationException("FAIL: value in storage is incorrect");
                        blocks.Remove(key);
                    }
                }

                stateManager.Approve();
                
                var inMemPhase = TimeUtils.CurrentTimeMillis();
                Console.WriteLine($"{T} insertions in {inMemPhase - start}ms");
                Console.WriteLine($"{(double) (inMemPhase - start) / T}ms per insertion");
                Console.WriteLine($"{(double) T * 1000 / (inMemPhase - start)} insertions per sec");

                stateManager.Commit();
                var commit = TimeUtils.CurrentTimeMillis();
            
                Console.WriteLine($"Commited {T} operations in {commit - inMemPhase}ms");
            }
        }
    }
}