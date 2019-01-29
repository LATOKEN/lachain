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
                Wasm = ByteString.CopyFrom("0061736d01000000011b056000017f60017f017f60027f7f0060057f7f7f7f7f017f600000026e0603656e760b67657463616c6c73697a65000003656e760c67657463616c6c76616c7565000103656e760e696e766f6b65636f6e7472616374000303656e760b6c6f616473746f72616765000203656e760b7361766573746f72616765000203656e760877726974656c6f67000203030201040404017000000503010001071f03066d656d6f727902000a6765745f6f6666736574000605737461727400070adc01020a00200041027441106a0bce0101037f024010004101480d00024010004104480d004100210041002101410021020340200210012000742001722101200041086a2100200241016a22024104470d000b200141effdb6f57d470d00410041dedbfafd7e3602104100100622004104100541004100360210200041201006220210032002412010050f0b410041bab9aff50136021041001006410410050f0b410041cafdebf57b36029001410010062200412010061004410041effdb6f57d3602244100410036029001200041044105100620004106100610021a0b".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract.Wasm.ToByteArray()))
            {
                throw new RuntimeException("Unable to validate smart-contract code");
            }

            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(contract.Hash, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
            var currentTime = TimeUtils.CurrentTimeMillis();
            stateManager.NewSnapshot();
            if (virtualMachine.InvokeContract(contract, UInt160Utils.Zero, new byte[] { }) != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed");
            }   
            stateManager.Approve();
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
        }
    }
}