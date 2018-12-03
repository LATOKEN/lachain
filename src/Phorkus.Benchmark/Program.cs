using System;
using System.Linq;
using Phorkus.Core.DI;

namespace Phorkus.Benchmark
{
    public class Application
    {
        internal static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet Phorkus.Benchmark.dll <storage/blockchain>");
                return;
            }

            var bench = args[0];
            IBootstrapper app;
            switch (bench)
            {
                case "blockchain":
                    app = new BlockchainBenchmark();
                    break;
                case "storage":
                    app = new StorageBenchmark();
                    break;
                default:
                    throw new Exception("Invalid benchmark type");
            }
            app.Start(args.Skip(1).ToArray());
        }
    }
}