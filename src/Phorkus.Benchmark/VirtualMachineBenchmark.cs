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
                Wasm = ByteString.CopyFrom("0061736d010000000117056000017f60027f7f0060037f7f7f0060017f00600000028e010803656e760d6765745f63616c6c5f73697a65000003656e760977726974655f6c6f67000103656e760f636f70795f63616c6c5f76616c7565000203656e760468616c74000303656e760a7365745f72657475726e000103656e760a6765745f73656e646572000303656e760c6c6f61645f73746f72616765000203656e760c736176655f73746f72616765000203030204040405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400090a9c0a0202000b960a06027f047e027f047e017f017e23808080800041c0016b2200248080808000024010808080800041034b0d00200041effdb6f57d360220200041206a4104108180808000200041c0016a2480808080000f0b41004104200041046a108280808000200041046a4104108180808000024002400240024002400240024002402000280204220141dc9bd8c0014a0d00200141bbb996c87a460d01200141beda8beb7d460d02200141b3cffaca00470d051080808080001a4104410420004180016a1082808080004105108380808000200041c0016a2480808080000f0b200141b184828507460d02200141dde5e19d02460d03200141dd9bd8c001470d041080808080001a4104410420004180016a108280808000200041baf9aef501360220200041206a4104108480808000200041c0016a2480808080000f0b024010808080800041334b0d0041061083808080000b41044138200041206a108280808000200020002d00203a0008200020002800213600092000200029002537000d2000200028002d360015200020002f00313b0019200020002d00333a001b200029034c21022000290344210320002903342104200029033c2105200041a8016a41106a4100360200200041a8016a41086a4200370300200042003703a801200041a8016a10858080800020004180016a41186a2201420037030020004180016a41106a2206420037030020004180016a41086a220742003703002000420037038001200041a8016a411420004180016a10868080800020012903002002427f857c200629030022082003427f857c2209200854200729030022082005427f857c220a200854200029038001220b2004427f857c2208200b54220c200a200cad7c220a507172220c2009200cad7c2209507172ad7c210b200842017c210d2008427f510d04420021080c050b1080808080001a4104410420004180016a1082808080004105108380808000200041c0016a2480808080000f0b024010808080800041134b0d0041061083808080000b4104411820004180016a108280808000200020002d0080013a0060200020002800810136006120002000290085013700652000200028008d0136006d200020002f0091013b0071200020002d0093013a0073200041206a41186a4200370300200041306a4200370300200041286a420037030020004200370320200041e0006a4114200041206a108680808000200041206a4120108480808000200041c0016a2480808080000f0b1080808080001a4104410420004180016a1082808080004105108380808000200041c0016a2480808080000f0b4105108380808000200041c0016a2480808080000f0b42002108200a42017c220a4200520d00200942017c220950ad21084200210a0b2001200b20087c370300200620093703002007200a3703002000200d37038001200041a8016a411420004180016a108780808000200041e0006a41186a22014200370300200041e0006a41106a22064200370300200041e0006a41086a2207420037030020004200370360200041086a4114200041e0006a1086808080002007290300210820002000290360220a20047c22043703602007200820057c22052004200a54220cad7c220437030020062006290300220a20037c22032005200854200c20045071722207ad7c22043703002001200129030020027c2003200a5420072004507172ad7c370300200041086a4114200041e0006a108780808000200041013a005f200041df006a4101108480808000200041c0016a2480808080000b008801046e616d650180010a000d6765745f63616c6c5f73697a65010977726974655f6c6f67020f636f70795f63616c6c5f76616c7565030468616c74040a7365745f72657475726e050a6765745f73656e646572060c6c6f61645f73746f72616765070c736176655f73746f7261676508115f5f7761736d5f63616c6c5f63746f72730905737461727400250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
            };
            if (!virtualMachine.VerifyContract(contract.Wasm.ToByteArray()))
                throw new RuntimeException("Unable to validate smart-contract code");
            
            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, contract);
            stateManager.Approve();
            
            Console.WriteLine("Contract Hash: " + hash.Buffer.ToHex());
            
            var currentTime = TimeUtils.CurrentTimeMillis();
            stateManager.NewSnapshot();

            var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
            var to = "0xfd893ce89186fc6861d339cb6ab5d75458e3daf3".HexToBytes().ToUInt160();
            
            /* give to sender 1 token */
            var valueToTransfer = Money.Wei;
            stateManager.CurrentSnapshot.Storage.SetValue(contract.Hash, sender.ToUInt256(), (valueToTransfer * 3).ToUInt256());
            
            /* ERC-20: totalSupply (0x18160ddd) */
            Console.WriteLine("\nERC-20: totalSupply()");
            var input = ContractEncoder.Encode("totalSupply()", sender);
            var status = virtualMachine.InvokeContract(contract, sender, input);
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: balanceOf (0x70a08231) */
            Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
            input = ContractEncoder.Encode("balanceOf(address)", sender);
            status = virtualMachine.InvokeContract(contract, sender, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: transfer (0xa9059cbb) */
            Console.WriteLine($"\nERC-20: transfer({to.Buffer.ToHex()}, {valueToTransfer})");
            input = ContractEncoder.Encode("transfer(address,uint256)", to, valueToTransfer);
            status = virtualMachine.InvokeContract(contract, sender, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: balanceOf (0x70a08231) */
            Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
            input = ContractEncoder.Encode("balanceOf(address)", sender);
            status = virtualMachine.InvokeContract(contract, sender, input);
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: balanceOf (0x70a08231) */
            Console.WriteLine($"\nERC-20: balanceOf({to.Buffer.ToHex()})");
            input = ContractEncoder.Encode("balanceOf(address)", to);
            status = virtualMachine.InvokeContract(contract, sender, input);
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