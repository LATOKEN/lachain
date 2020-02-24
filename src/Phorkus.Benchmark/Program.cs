using System;
using System.Linq;
using Phorkus.Core.DI;
using Phorkus.Crypto.MCL.BLS12_381;

namespace Phorkus.Benchmark
{
    public class Application
    {
        internal static void Main(string[] args)
        {
            Mcl.Init();
            if (args.Length == 0)
            {
                args = new[] { "blockchain" };
//                Console.WriteLine("Usage: dotnet Phorkus.Benchmark.dll <storage/blockchain/virtualmachine>");
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
            app.Start(args.Skip(1).ToArray());
        }
    }
}