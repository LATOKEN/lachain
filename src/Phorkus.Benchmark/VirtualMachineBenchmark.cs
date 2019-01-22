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
                Wasm = ByteString.CopyFrom("0061736d01000000010a0260017f0060017f017f02630503656e761261626f7274537461636b4f766572666c6f77000003656e760d5f5f6d656d6f72795f62617365037f0003656e760c5f5f7461626c655f62617365037f0003656e76066d656d6f727902018002800203656e76057461626c650170010000030201010617037f0141a0100b7f0141a090c0020b7d0143000000000b070801045f66696200010901000a8a0101870101127f23022112230241206a2402230223034e0440412010000b200021014100210b4101210c41002109034002402009210d2001210e200d200e48210f200f4504400c010b200b2110200c2102201020026a21032003210a200c21042004210b200a21052005210c20092106200641016a2107200721090c010b0b200c21082012240220080f0b".HexToBytes())
            };
            const int tries = 100;
            var invocations = new Invocation[tries];
            for (int i = 0; i < tries; i++)
                invocations[i] = new Invocation
                {
                    ContractHash = UInt160Utils.Zero,
                    MethodName = "factorial",
                    Params = { ByteString.CopyFrom(BitConverter.GetBytes(0xffffff - i)) }
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
                for (var j = 0; j < 0xffffff - i; j++) {
                    result = result * result % 1000000007;
                }
                Console.WriteLine("C# result: " + result);
            }
            elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Avg. Elapsed Time: " + elapsedTime / (tries - 1) + "ms");
        }
    }
}