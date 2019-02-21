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
                ByteCode = ByteString.CopyFrom("0061736d0100000001090260027f7f0060000002120103656e760a7365745f72657475726e000003030201010405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400020af90a0202000bf30a07027f027e017f077e017f017e097f2380808080004180016b2200248080808000200041c0006a410872210142b32b2102420021034100210442002105420021060340200041c0006a41386a20034220882207370300200041c0006a41306a200342ffffffff0f832208370300200041c0006a41286a20054220882209370300200041c0006a41206a200542ffffffff0f83220a370300200041c0006a41186a2006422088220b370300200041c0006a41106a200642ffffffff0f832206370300200041386a220c2007200242ffffffff0f8322037e220d42ffffffff0f83200820037e22054220887c370300200041306a220e200542ffffffff0f83200920037e22054220887c370300200041286a220f200542ffffffff0f83200a20037e22054220887c370300200041206a2210200542ffffffff0f83200b20037e22054220887c370300200041186a2211200542ffffffff0f83200620037e22054220887c370300200041106a2212200542ffffffff0f832002422088220720037e22024220887c37030020002007370348200020033703402000200320037e220542ffffffff0f833703002000200242ffffffff0f8320054220887c220237030841002113200321050340200020136a221441086a2002200520077e220542ffffffff0f837c370300024020134130460d00201441106a2215201529030020054220887c3703000b024020134130460d00200120136a2903002105201441106a2903002102201341086a21130c010b0b4100211320032102410021140340200020136a221641106a22152015290300200220067e220242ffffffff0f837c370300024020134128460d00201641186a2215201529030020024220887c3703000b0240201441016a22144106460d00200120136a2903002102201341086a21130c010b0b4100211320032102410021140340200020136a221641186a221520152903002002200b7e220242ffffffff0f837c370300024020134120460d00201641206a2215201529030020024220887c3703000b0240201441016a22144105460d00200120136a2903002102201341086a21130c010b0b4100211320032102410021140340200020136a221641206a221520152903002002200a7e220242ffffffff0f837c370300024020134118460d00201641286a2215201529030020024220887c3703000b0240201441016a22144104460d00200120136a2903002102201341086a21130c010b0b4100211320032102410021140340200020136a221641286a22152015290300200220097e220242ffffffff0f837c370300024020134110460d00201641306a2215201529030020024220887c3703000b0240201441016a22144103460d00200120136a2903002102201341086a21130c010b0b41002113410021140340200020136a221641306a22152015290300200320087e220342ffffffff0f837c370300024020134108460d00201641386a2215201529030020034220887c3703000b0240201441016a22144102460d00200120136a2903002103201341086a21130c010b0b201220122903002000290308200029030022034220887c22024220887c220542ffffffff0f8322083703002011201129030020054220887c220542ffffffff0f8322093703002010201029030020054220887c220542ffffffff0f83220a3703002000200242ffffffff0f8322023703082000200342ffffffff0f8322033703002002422086200384210220094220862008842106200f29030020054220887c2203422086200a842105200e29030020034220887c200c290300200d7c4220867c2103200441016a22044180ade204470d000b20002003370358200020053703502000200637034820002002370340200041c0006a412010808080800020004180016a2480808080000b002e046e616d65012703000a7365745f72657475726e01115f5f7761736d5f63616c6c5f63746f72730205737461727400250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
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