using System;
using Google.Protobuf;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Core.VM;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility;
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
                ContractAddress = hash,
                ByteCode = ByteString.CopyFrom("0061736d0100000001090260027f7f0060000002120103656e760a7365745f72657475726e000003030201010405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400020a7b0202000b7603017f017e017f23808080800041206b220024808080800042b32b21014180c2d72f21020340200120017e428794ebdc038221012002417f6a22020d000b200041186a4200370300200041106a4200370300200042003703082000200137030020004120108080808000200041206a2480808080000b002e046e616d65012703000a7365745f72657475726e01115f5f7761736d5f63616c6c5f63746f72730205737461727400250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract.ByteCode.ToByteArray()))
                throw new RuntimeException("Unable to validate smart-contract code");            
            
            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
            for (var i = 0; i < 3; ++i)
            {
                var currentTime = TimeUtils.CurrentTimeMillis();
                stateManager.NewSnapshot();

                var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
                var to = "0xfd893ce89186fc6861d339cb6ab5d75458e3daf3".HexToBytes().ToUInt160();
            
                /* give to sender 1 token */
                var valueToTransfer = Money.Wei;
                stateManager.CurrentSnapshot.Storage.SetValue(contract.ContractAddress, sender.ToUInt256(), (valueToTransfer * 3).ToUInt256());
                var context = new InvocationContext(sender);
            
//                /* ERC-20: totalSupply (0x18160ddd) */
//                Console.WriteLine("\nERC-20: totalSupply()");
//                var input = ContractEncoder.Encode("totalSupply()");
//                Console.WriteLine("ABI: " + input.ToHex());
//                var status = virtualMachine.InvokeContract(contract, context, input);
//                if (status != ExecutionStatus.Ok)
//                {
//                    stateManager.Rollback();
//                    Console.WriteLine("Contract execution failed: " + status);
//                    return;
//                }
                
//                /* ERC-20: balanceOf (0x40c10f19) */
//                Console.WriteLine($"\nERC-20: mint({sender.Buffer.ToHex()},{Money.FromDecimal(100)})");
//                var input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(100));
//                Console.WriteLine("ABI: " + input.ToHex());
//                var status = virtualMachine.InvokeContract(contract, context, input);
//                if (status != ExecutionStatus.Ok)
//                {
//                    stateManager.Rollback();
//                    Console.WriteLine("Contract execution failed: " + status);
//                    goto exit_mark;
//                }

//                /* ERC-20: totalSupply (0x18160ddd) */
//                Console.WriteLine("\nERC-20: totalSupply()");
//                Console.WriteLine("ABI: " + input.ToHex());
//                input = ContractEncoder.Encode("totalSupply()");
//                status = virtualMachine.InvokeContract(contract, context, input);
//                if (status != ExecutionStatus.Ok)
//                {
//                    stateManager.Rollback();
//                    Console.WriteLine("Contract execution failed: " + status);
//                    goto exit_mark;
//                }

                /* ERC-20: balanceOf (0x0a08231) */
                Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
                var input = ContractEncoder.Encode("balanceOf(address)", sender);
                Console.WriteLine("ABI: " + input.ToHex());
                var result = virtualMachine.InvokeContract(contract, context, input, 100_000_000_000_000UL);
                if (result.Status != ExecutionStatus.Ok)
                {
                    stateManager.Rollback();
                    Console.WriteLine("Contract execution failed: " + result.Status + ", gasUsed=" + result.GasUsed);
                    goto exit_mark;
                }
                
                stateManager.Approve();
                exit_mark:
                var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
                Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
            }
        }
    }
}