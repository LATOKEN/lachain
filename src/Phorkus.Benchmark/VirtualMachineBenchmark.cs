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
                Hash = hash,
                Version = ContractVersion.Wasm,
                Wasm = ByteString.CopyFrom("0061736d01000000011a0560027f7f0060000060027f7e017f60037f7f7f0060017f017f02120103656e760a7365745f72657475726e00000307060101020304040405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400020aaf030602000b5601017f23808080800041e0006b22002480808080002000200041c0006a42e400108380808000200041206a421410838080800010848080800020001085808080004120108080808000200041e0006a2480808080000b26002000420037030820002001370300200041186a4200370300200041106a420037030020000bfc0101047e200010868080800021002002290300220320012903007c2204200029030022057c21060240024020042003540d00200542005220065071450d010b2000200029030842017c3703080b200020063703002002290308220320012903087c2204200029030822057c21060240024020042003540d00200542005220065071450d010b2000200029031042017c3703100b200041086a20063703002002290310220320012903107c2204200029031022057c21060240024020042003540d00200542005220065071450d010b2000200029031842017c3703180b200041106a20063703002000200229031820012903187c20002903187c3703180b040020000b290020004200370300200041186a4200370300200041106a4200370300200041086a420037030020000b00ac01046e616d6501a40107000a7365745f72657475726e01115f5f7761736d5f63616c6c5f63746f727302057374617274032475696e743235363a3a75696e7432353628756e7369676e6564206c6f6e67206c6f6e6729042875696e743235363a3a6f70657261746f722b2875696e7432353620636f6e7374262920636f6e737405176765745f6f666673657428766f696420636f6e73742a29061275696e743235363a3a75696e74323536282900250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract.Wasm.ToByteArray()))
                throw new RuntimeException("Unable to validate smart-contract code");
            
            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
            var currentTime = TimeUtils.CurrentTimeMillis();
            stateManager.NewSnapshot();
            /*var status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, new byte[] { }); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }*/

            var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
            var to = "0xfd893ce89186fc6861d339cb6ab5d75458e3daf3".HexToBytes().ToUInt160();
            
//            /* give to sender 1 token */
//            stateManager.CurrentSnapshot.Storage.SetValue(contract.Hash, sender.ToUInt256(), Money.FromDecimal(1).ToUInt256());
            
            /* ERC-20: totalSupply (0x18160ddd) */
            var input = new byte[24];
            input[3] = 0x18;
            input[2] = 0x16;
            input[1] = 0x0d;
            input[0] = 0xdd;
            for (var i = 0; i < 20; i++)
                input[i + 4] = sender.Buffer[i];
            var status = virtualMachine.InvokeContract(contract, sender, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
//            /* ERC-20: balanceOf (0x70a08231) */
//            input = new byte[24];
//            input[3] = 0x70;
//            input[2] = 0xa0;
//            input[1] = 0x82;
//            input[0] = 0x31;
//            for (var i = 0; i < 20; i++)
//                input[i + 4] = sender.Buffer[i];
//            status = virtualMachine.InvokeContract(contract, sender, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
//            
//            /* ERC-20: transfer (0xa9059cbb) */
//            input = new byte[4 + 20 + 4];
//            input[3] = 0xa9;
//            input[2] = 0x05;
//            input[1] = 0x9c;
//            input[0] = 0xbb;
//            for (var i = 0; i < 20; i++)
//                input[i + 4] = to.Buffer[i];
//            input[24] = 0x00;
//            input[25] = 0x00;
//            input[26] = 0x00;
//            input[27] = 0x01;
//            status = virtualMachine.InvokeContract(contract, sender, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
            
//            /* ERC-20: balanceOf (0x70a08231) */
//            input = new byte[24];
//            input[3] = 0x70;
//            input[2] = 0xa0;
//            input[1] = 0x82;
//            input[0] = 0x31;
//            status = virtualMachine.InvokeContract(contract, sender, input);
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
            
//            input = new byte[24 + 32];
//            input[0] = 2;
//            input[24] = 10;
//            status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
//            
//            input = new byte[24];
//            input[0] = 0xff;
//            status = virtualMachine.InvokeContract(contract, UInt160Utils.Zero, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
            
            stateManager.Approve();
            exit_mark:
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
        }
    }
}