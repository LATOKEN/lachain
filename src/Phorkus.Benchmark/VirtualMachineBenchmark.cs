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
            
            /*var contract = new Contract
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
                Wasm = ByteString.CopyFrom("0061736d01000000011b0560027f7f0060057f7f7f7f7f017f60017f017f60017f0060000002160203656e760463616c6c000103656e76036c6f6700000304030203040404017000000503010001072704066d656d6f727902000a6765745f6f66667365740002057072696e74000305737461727400040a8e01030a00200041027441106a0b5301037f0240024020002d00002202450d00200041016a2101410021000340200041a0036a20023a0000200120006a2102200041016a2203210020022d000022020d000c020b0b410021030b41a003200310010b2d01017f41a005100341004284b6ca99f5ddefd65e37022441001002220041044106100220004107100210001a0b0b14010041a0050b0d48656c6c6f20776f726c642100".HexToBytes())
            };
            const int tries = 5;
            var invocations = new Invocation[tries];
            for (var i = 0; i < tries; i++)
                invocations[i] = new Invocation
                {
                    ContractAddress = UInt160Utils.Zero,
                    MethodName = "factorial",
                    Input = ByteString.CopyFrom(BitConverter.GetBytes(0xfffffff - i))
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
                if (virtualMachine.InvokeContract(contract, invocations[i]) != ExecutionStatus.Ok)
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
            Console.WriteLine("Avg. Elapsed Time: " + elapsedTime / (tries - 1) + "ms");*/
        }
    }
}