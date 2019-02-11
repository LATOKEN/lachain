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
                Wasm = ByteString.CopyFrom("0061736d010000000117056000017f60027f7f0060037f7f7f0060017f00600000028e010803656e760d6765745f63616c6c5f73697a65000003656e760977726974655f6c6f67000103656e760f636f70795f63616c6c5f76616c7565000203656e760468616c74000303656e760c6c6f61645f73746f72616765000203656e760a7365745f72657475726e000103656e760a6765745f73656e646572000303656e760c736176655f73746f72616765000203030204040405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400090adf110202000bd91104027f047e037f057e23808080800041e0016b2200248080808000024010808080800041034b0d00200041effdb6f57d360210200041106a4104108180808000200041e0016a2480808080000f0b410041042000410c6a1082808080002000410c6a4104108180808000024002400240024002400240024002400240024002400240200028020c220141dc9bd8c0014c0d00200141989e8486044a0d01200141dd9bd8c001460d02200141dde5e19d02470d061080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b200141bbb996c87a460d02200141beda8beb7d460d03200141b3cffaca00470d051080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b200141999e848604460d03200141b184828507470d04024010808080800041134b0d0041061083808080000b41044118200041c0016a108280808000200020002d00c0013a008801200020002800c10136008901200020002900c50137008d01200020002800cd0136009501200020002f00d1013b009901200020002d00d3013a009b01200041106a41186a4200370300200041206a4200370300200041186a42003703002000420037031020004188016a4114200041106a108480808000200041106a4120108580808000200041e0016a2480808080000f0b1080808080001a41044104200041c0016a108280808000200041baf9aef501360210200041106a4104108580808000200041e0016a2480808080000f0b024010808080800041334b0d0041061083808080000b41044138200041106a108280808000200020002d00103a005020002000280011360051200020002900153700552000200028001d36005d200020002f00213b0061200020002d00233a0063200029033c21022000290334210320002903242104200029032c2105200041a8016a41106a4100360200200041a8016a41086a4200370300200042003703a801200041a8016a10868080800020004188016a41186a2201420037030020004188016a41106a2206420037030020004188016a41086a220742003703002000420037038801200041a8016a411420004188016a108480808000200041c0016a41186a4200370300200041c0016a41106a4200370300200041c0016a41086a22084200370300200042003703c00120012903002002427f857c200629030022092003427f857c220a200954200729030022092005427f857c220b20095420002903880122092004427f857c220c2009542201200b2001ad7c220b5071722201200a2001ad7c220a507172ad7c2109200c42017c210d200c427f510d032000200d3703c0010c040b1080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b024010808080800041334b0d0041061083808080000b41044138200041106a108280808000200020002d00103a0068200020002800113600692000200029001537006d2000200028001d360075200020002f00213b0079200020002d00233a007b200029033c210220002903342105200029032c21092000290324210c4200210420004188016a41186a420037030020004188016a41106a420037030020004188016a41086a42003703002000420037038801200041e8006a411420004188016a108480808000200041c0016a41186a4200370300200041c0016a41106a4200370300200041c0016a41086a22014200370300200042003703c001420021030240200c2000290388017c220b200c5a0d0042012103200142013703000b2000200b3703c00120092000290390017c220b20037c210c02400240200b2009540d002003420052200c5071450d010b42012104200041d0016a42013703000b200041c8016a200c37030020052000290398017c220920047c21030240024020092005540d0042002105200442005220035071450d010b42012105200041d8016a42013703000b200041c0016a41106a200337030020004188016a41106a2003370300200041d8016a200220002903a0017c20057c220337030020004188016a41086a200041c0016a41086a290300370300200020002903c00137038801200020033703a001200041e8006a411420004188016a108780808000200041e0016a2480808080000f0b4105108380808000200041e0016a2480808080000f0b200842013703002000200d3703c001200b42017c220b500d010b200041c8016a200b3703000c010b200041c8016a200b370300200041d0016a22014201370300200a42017c220a50450d002001200a370300200041d8016a4201370300200942017c21090c010b200041d0016a200a3703000b200041c0016a41186a2206200937030020004188016a41186a200937030020004188016a41086a200041c0016a41086a220129030037030020004188016a41106a200041c0016a41106a2207290300370300200020002903c00137038801200041a8016a411420004188016a10878080800042002109200041e8006a41186a4200370300200041e8006a41106a4200370300200041e8006a41086a420037030020004200370368200041d0006a4114200041e8006a108480808000200642003703002007420037030020014200370300200042003703c0014200210c0240200420002903687c220b20045a0d004201210c200142013703000b2000200b3703c001200520002903707c220b200c7c210402400240200b2005540d00200c42005220045071450d010b42012109200041d0016a42013703000b200041c8016a2004370300200320002903787c220520097c21040240024020052003540d0042002103200942005220045071450d010b42012103200041d8016a42013703000b200041c0016a41106a2004370300200041e8006a41106a2004370300200041d8016a20022000290380017c20037c2203370300200041e8006a41086a200041c0016a41086a290300370300200020002903c0013703682000200337038001200041d0006a4114200041e8006a108780808000200041013a00c001200041c0016a4101108580808000200041e0016a2480808080000b008801046e616d650180010a000d6765745f63616c6c5f73697a65010977726974655f6c6f67020f636f70795f63616c6c5f76616c7565030468616c74040c6c6f61645f73746f72616765050a7365745f72657475726e060a6765745f73656e646572070c736176655f73746f7261676508115f5f7761736d5f63616c6c5f63746f72730905737461727400250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
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

            /* ERC-20: balanceOf (0x70a08231) */
            Console.WriteLine($"\nERC-20: mint({sender.Buffer.ToHex()},{Money.FromDecimal(1)})");
            input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(1));
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
            
            stateManager.Approve();
            exit_mark:
            var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
            Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
        }
    }
}