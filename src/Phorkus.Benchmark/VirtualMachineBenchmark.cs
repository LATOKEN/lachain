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
                Wasm = ByteString.CopyFrom("0061736d010000000117056000017f60027f7f0060037f7f7f0060017f00600000028e010803656e760d6765745f63616c6c5f73697a65000003656e760977726974655f6c6f67000103656e760f636f70795f63616c6c5f76616c7565000203656e760468616c74000303656e760c6c6f61645f73746f72616765000203656e760c736176655f73746f72616765000203656e760a7365745f72657475726e000103656e760a6765745f73656e646572000303030204040405017001010105030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400090adf110202000bd91104027f077e037f027e23808080800041e0016b2200248080808000024010808080800041034b0d00200041effdb6f57d360210200041106a4104108180808000200041e0016a2480808080000f0b410041042000410c6a1082808080002000410c6a4104108180808000024002400240024002400240024002400240024002400240200028020c220141a2f0caeb7d4c0d00200141efc08a8c034a0d01200141a3f0caeb7d460d02200141c082bfc801470d06024010808080800041334b0d0041061083808080000b41044138200041106a108280808000200020002d00103a0068200020002800113600692000200029001537006d2000200028001d360075200020002f00213b0079200020002d00233a007b200029033c210220002903342103200029032c2104200029032421054200210620004188016a41186a420037030020004188016a41106a420037030020004188016a41086a42003703002000420037038801200041e8006a411420004188016a108480808000200041c0016a41186a4200370300200041c0016a41106a4200370300200041c0016a41086a22014200370300200042003703c00142002107024020052000290388017c220820055a0d0042012107200142013703000b200020083703c00120042000290390017c220820077c21050240024020082004540d00200742005220055071450d010b42012106200041d0016a42013703000b200041c8016a200537030020032000290398017c220420067c21070240024020042003540d0042002103200642005220075071450d010b42012103200041d8016a42013703000b200041c0016a41106a200737030020004188016a41106a2007370300200041d8016a200220002903a0017c20037c220737030020004188016a41086a200041c0016a41086a290300370300200020002903c00137038801200020073703a001200041e8006a411420004188016a108580808000200041e0016a2480808080000f0b20014189bc9d9d7b460d02200141a98bf0dc7b460d0320014198acb4e87d470d051080808080001a41044104200041c0016a108280808000200041bab9aff501360210200041106a4104108680808000200041e0016a2480808080000f0b200141f0c08a8c03460d03200141ddc5b5f703470d041080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b1080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b1080808080001a41044104200041c0016a1082808080004105108380808000200041e0016a2480808080000f0b024010808080800041334b0d0041061083808080000b41044138200041106a108280808000200020002d00103a005020002000280011360051200020002900153700552000200028001d36005d200020002f00213b0061200020002d00233a0063200029033c21022000290334210720002903242106200029032c2103200041a8016a41106a4100360200200041a8016a41086a4200370300200042003703a801200041a8016a10878080800020004188016a41186a2201420037030020004188016a41106a2209420037030020004188016a41086a220a42003703002000420037038801200041a8016a411420004188016a108480808000200041c0016a41186a4200370300200041c0016a41106a4200370300200041c0016a41086a220b4200370300200042003703c00120012903002002427f857c200929030022042007427f857c220c200454200a29030022042003427f857c220820045420002903880122042006427f857c2205200454220120082001ad7c22085071722201200c2001ad7c220c507172ad7c2104200542017c210d2005427f510d022000200d3703c0010c030b024010808080800041134b0d0041061083808080000b41044118200041c0016a108280808000200020002d00c0013a008801200020002800c10136008901200020002900c50137008d01200020002800cd0136009501200020002f00d1013b009901200020002d00d3013a009b01200041106a41186a4200370300200041206a4200370300200041186a42003703002000420037031020004188016a4114200041106a108480808000200041106a4120108680808000200041e0016a2480808080000f0b4105108380808000200041e0016a2480808080000f0b200b42013703002000200d3703c001200842017c2208500d010b200041c8016a20083703000c010b200041c8016a2008370300200041d0016a22014201370300200c42017c220c50450d002001200c370300200041d8016a4201370300200442017c21040c010b200041d0016a200c3703000b200041c0016a41186a2209200437030020004188016a41186a200437030020004188016a41086a200041c0016a41086a220129030037030020004188016a41106a200041c0016a41106a220a290300370300200020002903c00137038801200041a8016a411420004188016a10858080800042002104200041e8006a41186a4200370300200041e8006a41106a4200370300200041e8006a41086a420037030020004200370368200041d0006a4114200041e8006a10848080800020094200370300200a420037030020014200370300200042003703c001420021050240200620002903687c220820065a0d0042012105200142013703000b200020083703c001200320002903707c220820057c21060240024020082003540d00200542005220065071450d010b42012104200041d0016a42013703000b200041c8016a2006370300200720002903787c220320047c21060240024020032007540d0042002107200442005220065071450d010b42012107200041d8016a42013703000b200041c0016a41106a2006370300200041e8006a41106a2006370300200041d8016a20022000290380017c20077c2207370300200041e8006a41086a200041c0016a41086a290300370300200020002903c0013703682000200737038001200041d0006a4114200041e8006a108580808000200041013a00c001200041c0016a4101108680808000200041e0016a2480808080000b008801046e616d650180010a000d6765745f63616c6c5f73697a65010977726974655f6c6f67020f636f70795f63616c6c5f76616c7565030468616c74040c6c6f61645f73746f72616765050c736176655f73746f72616765060a7365745f72657475726e070a6765745f73656e64657208115f5f7761736d5f63616c6c5f63746f72730905737461727400250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
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
            Console.WriteLine("ABI: " + input.ToHex());
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

            /* ERC-20: balanceOf (0x40c10f19) */
            Console.WriteLine($"\nERC-20: mint({sender.Buffer.ToHex()},{Money.FromDecimal(1)})");
            input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(1));
            status = virtualMachine.InvokeContract(contract, sender, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: balanceOf (0x0a08231) */
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