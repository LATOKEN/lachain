using System;
using System.Linq;
using Lachain.Core.CLI;
using Lachain.Core.DI;

namespace Lachain.Benchmark
{
    public class Application
    {
        internal static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = new[] { "blockchain" };
//                Console.WriteLine("Usage: dotnet Lachain.Benchmark.dll <storage/blockchain/virtualmachine>");
//                return;
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
                case "virtualmachine":
                    app = new VirtualMachineBenchmark();
                    break;
                default:
                    throw new Exception("Invalid benchmark type");
            }
            app.Start(new RunOptions());
        }
    }
}