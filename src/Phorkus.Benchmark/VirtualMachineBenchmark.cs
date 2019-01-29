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
                Wasm = ByteString.CopyFrom("0061736d0100000001180560027f7f006000017f60037f7f7f0060017f017f600000026a0603656e760d636f707963616c6c76616c7565000203656e760b67657463616c6c73697a65000103656e760b6c6f616473746f72616765000003656e760b7361766573746f72616765000003656e760973657472657475726e000003656e760877726974656c6f6700000304030304040404017000000503010001072a04066d656d6f727902000a6765745f6f666673657400060866616c6c6261636b000705737461727400080aa801030a00200041027441106a0b1500410041dedbfafd7e36021041001006410410050b840101027f02400240100141034c0d00410041044100100622001000410028021022014102460d0120014101470d00024010014118460d0010070b4104411820001000200041201006220110022001412010040f0b10070f0b024010014138460d0010070b41044118200010004118413841201006220110002000200110032001412010040b".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract.Wasm.ToByteArray()))
            {
                throw new RuntimeException("Unable to validate smart-contract code");
            }

            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
            var currentTime = TimeUtils.CurrentTimeMillis();
            stateManager.NewSnapshot();
            var status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, new byte[] { }); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }

            var input = new byte[24];
            input[0] = 1;
            status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            input = new byte[24 + 32];
            input[0] = 2;
            input[24] = 10;
            status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            input = new byte[24];
            input[0] = 1;
            status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            stateManager.Approve();
            exit_mark:
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
        }
    }
}