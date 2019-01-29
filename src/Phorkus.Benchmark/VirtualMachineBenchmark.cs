using System;
using Google.Protobuf;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;
using Phorkus.WebAssembly;

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
            var stateManager = _container.Resolve<IStateManager>();
            
            var hash = UInt160Utils.Zero;
            var contract = new Contract
            {
                Hash = hash,
                Version = ContractVersion.Wasm,
                Wasm = ByteString.CopyFrom("0061736d01000000011b056000017f60017f017f60027f7f0060057f7f7f7f7f017f60000002520503656e760463616c6c000303656e760b67657463616c6c73697a65000003656e760c67657463616c6c76616c7565000103656e760b73746f726167656c6f6164000203656e760877726974656c6f67000203030201040404017000000503010001071f03066d656d6f727902000a6765745f6f6666736574000505737461727400060ab701020a00200041027441106a0ba90101037f024010014101480d00024010014104480d004100210041002101410021020340200210022000742001722101200041086a2100200241016a22024104470d000b200141effdb6f57d470d00410041effdb6f57d36021041001005410410040f0b4100419ed6f2d67b36021041001005410410040f0b410041effdb6f57d36022441001005220041044105100520004106100510001a200041201005220210032002412010040b".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract))
            {
                throw new RuntimeException("Unable to validate smart-contract code");
            }

            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(contract.Hash, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
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