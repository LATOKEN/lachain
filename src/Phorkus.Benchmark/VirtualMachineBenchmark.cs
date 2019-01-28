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
                Wasm = ByteString.CopyFrom("0061736d01000000011f0660027f7f006000017f60017f017f60057f7f7f7f7f017f60017f0060000002400403656e760463616c6c000303656e760b67657463616c6c73697a65000103656e760c67657463616c6c76616c7565000203656e760877726974656c6f6700000304030204050404017000000503010001072704066d656d6f727902000a6765745f6f66667365740004057072696e74000505737461727400060ae401030a00200041027441106a0b5301037f0240024020002d00002202450d00200041016a2101410021000340200041a0036a20023a0000200120006a2102200041016a2203210020022d000022020d000c020b0b410021030b41a003200310030b820101037f024010014101480d00024010014104480d004100210041002101410021020340200210022000742001722101200041086a2100200241016a22024104470d000b200141effdb6f57d470d0041a00510050f0b41c00510050f0b41d0051005410041effdb6f57d36022441001004220041044105100420004106100410001a0b0b5e030041a0050b1d48656c6c6f20776f726c642066726f6d203078646561646265656621000041c0050b0e576520617265206675636b6564000041d0050b2043616c6c696e672030786465616462656566206f66203078303030302e2e2e00".HexToBytes())
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