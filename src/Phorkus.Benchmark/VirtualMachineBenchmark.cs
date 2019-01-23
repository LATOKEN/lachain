using System;
using Google.Protobuf;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Benchmark
{
    public class VirtualMachineBenchmark : IBootstrapper
    {
        private readonly IContainer _container;
        
        public VirtualMachineBenchmark()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));

            containerBuilder.RegisterModule<LoggingModule>();
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<CryptographyModule>();
            containerBuilder.RegisterModule<MessagingModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }
        
        public void Start(string[] args)
        {
            var virtualMachine = _container.Resolve<IVirtualMachine>();
            
            var contract = new Contract
            {
                Hash = UInt160Utils.Zero,
                Abi =
                {
                    new ContractABI
                    {
                        Method = "factorial",
                        Input =
                        {
                            ContractType.Integer
                        },
                        Output = ContractType.Long
                    }
                },
                Version = ContractVersion.Wasm,
                Wasm = ByteString.CopyFrom("0061736d0100000001060160017f017f030201000404017000000503010001071602066d656d6f7279020009666163746f7269616c00000a38013601017e024020004101480d0042b9e00021010340200120017e428794ebdc038221012000417f6a22000d000b2001a70f0b41b9e0000b".HexToBytes())
            };
            const int tries = 5;
            var invocations = new Invocation[tries];
            for (var i = 0; i < tries; i++)
                invocations[i] = new Invocation
                {
                    ContractHash = UInt160Utils.Zero,
                    MethodName = "factorial",
                    Params = { ByteString.CopyFrom(BitConverter.GetBytes(0xfffffff - i)) }
                };
            var currentTime = TimeUtils.CurrentTimeMillis();
            for (var i = 0; i < tries; i++)
            {
                if (i == 1)
                {
                    var curT = TimeUtils.CurrentTimeMillis();
                    Console.WriteLine("First call: " + (curT - currentTime) + "ms");
                    currentTime = curT;
                }
                if (!virtualMachine.InvokeContract(contract, invocations[i]))
                    break;
            }
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            
            Console.WriteLine("Avg. Elapsed Time: " + elapsedTime / (tries - 1) + "ms");
            
            currentTime = TimeUtils.CurrentTimeMillis();
            for (var i = 0; i < tries; i++)
            {
                if (i == 1)
                    currentTime = TimeUtils.CurrentTimeMillis();
                long result = 12345;
                for (var j = 0; j < 0xfffffff - i; j++) {
                    result = result * result % 1000000007;
                }
                Console.WriteLine("C# result: " + result);
            }
            elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Avg. Elapsed Time: " + elapsedTime / (tries - 1) + "ms");
        }
    }
}