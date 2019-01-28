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
                Wasm = ByteString.CopyFrom("0061736d01000000011f0660027f7f006000017f60017f017f60057f7f7f7f7f017f60017f0060000002400403656e760463616c6c000303656e760b67657463616c6c73697a65000103656e760c67657463616c6c76616c7565000203656e760877726974656c6f6700000304030204050404017000000503010001072704066d656d6f727902000a6765745f6f66667365740004057072696e74000505737461727400060ade01030a00200041027441106a0b5301037f0240024020002d00002202450d00200041016a2101410021000340200041a0036a20023a0000200120006a2102200041016a2203210020022d000022020d000c020b0b410021030b41a003200310030b7d01037f024010014101480d00024010014104480d004100210041002101410021020340200210022000742001722101200041086a2100200241016a22024104470d000b200141effdb6f57d470d0041a00510050f0b41b00510050f0b410041effdb6f57d36022441001004220041044105100420004106100410001a0b0b28020041a0050b0d48656c6c6f20776f726c6421000041b0050b0e576520617265206675636b656400".HexToBytes())
            };
            var currentTime = TimeUtils.CurrentTimeMillis();
            if (virtualMachine.InvokeContract(contract, UInt160Utils.Zero, new byte[] { }) != ExecutionStatus.Ok)
            {
                Console.WriteLine("Contract execution failed");
            }   
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
        }
    }
}