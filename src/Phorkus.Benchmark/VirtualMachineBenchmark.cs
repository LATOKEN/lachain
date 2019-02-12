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
                Wasm = ByteString.CopyFrom("0061736d0100000001170560027f7f006000017f60037f7f7f0060017f0060000002ac010903656e760d6765745f63616c6c5f73697a65000103656e760f636f70795f63616c6c5f76616c7565000203656e760977726974655f6c6f67000003656e760b73797374656d5f68616c74000303656e760a7365745f72657475726e000003656e760a6765745f73656e646572000303656e761063727970746f5f6b656363616b323536000203656e760c6c6f61645f73746f72616765000203656e760c736176655f73746f7261676500020307060404000302000405017001030305030100020615037f01419088040b7f00419088040b7f004181080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e640302057374617274000a0908010041010b020b0e0acb150602000b8c0602027f037e2380808080004190016b220024808080800002400240024002400240024002400240024010808080800041034b0d002000410036020c0c010b41004104200041306a10818080800020002000280230220136020c2001450d002000410c6a4104108280808000200028020c220141a2f0caeb7d4c0d01200141efc08a8c034a0d02200141a3f0caeb7d460d03200141c082bfc801470d06418180808000108c8080800020004190016a2480808080000f0b410510838080800020004190016a2480808080000f0b20014189bc9d9d7b460d02200141a98bf0dc7b460d0520014198acb4e87d470d04024010808080800041034b0d0041061083808080000b41044104200041106a10818080800020004188016a410036020020004180016a420037030020004200370378200041306a41f500200041f8006a108d80808000200041306a412010848080800020004190016a2480808080000f0b200141f0c08a8c03460d02200141ddc5b5f703470d03024010808080800041034b0d0041061083808080000b41044104200041106a108180808000410510838080800020004190016a2480808080000f0b024010808080800041cb004b0d0041061083808080000b410441cc00200041306a108180808000410510838080800020004190016a2480808080000f0b024010808080800041034b0d0041061083808080000b41044104200041106a108180808000410510838080800020004190016a2480808080000f0b024010808080800041174b0d0041061083808080000b41044118200041106a10818080800020002903102102200029031821032000200028022036028801200020033703800120002002370378200041306a41002d008088808000200041f8006a108d8080800020002903302102200029033821032000290340210420002000290348370348200020043703402000200337033820002002370330200041306a412010848080800020004190016a2480808080000f0b410510838080800020004190016a2480808080000f0b418280808000108c8080800020004190016a2480808080000b960503017f087e017f23808080800041e0006b2202248080808000200241f7013a0020200220002d00003a002120022000280001360122200220002900053701262002200028000d36012e200220002f00113b0132200220002d00133a0034200241003b0035200241206a4117200210868080800020024120200241c0006a10878080800020022903582103200229035021042002290348210520012903082106200129031821072001290310210820022001290300220920022903407c220a3703402002200620057c2205200a200954220bad7c22093703482002200820047c22042005200654200b2009507172220bad7c22063703502002200720037c2004200854200b2006507172ad7c370358200241003b0035200241f7013a0020200220002d00003a002120022000280001360122200220002900053701262002200028000d36012e200220002f00113b0132200220002d00133a0034200241206a4117200210868080800020024120200241c0006a108880808000200241003600312002420037002920024200370021200241f5003a0020200241003b0035200241206a4117200210868080800020024120200241c0006a10878080800020022903582103200229035021042002290348210520012903082106200129031821072001290310210820022001290300220920022903407c220a3703402002200620057c2205200a2009542200ad7c22093703482002200820047c22042005200654200020095071722200ad7c22063703502002200720037c200420085420002006507172ad7c370358200241003600312002420037002920024200370021200241003b0035200241f5003a0020200241206a4117200210868080800020024120200241c0006a108880808000200241e0006a2480808080000bb80102017f067e23808080800041f0006b2201248080808000024010808080800041374b0d0041061083808080000b41044138200110818080800020012903142102200129031c210320012903242104200129032c2105200129030021062001290308210720012001280210360268200120073703602001200637035820012005370350200120043703482001200337034020012002370338200141d8006a200141386a200011808080800000200141f0006a2480808080000bb30101017f23808080800041e0006b2203248080808000200320013a0040200320022d00003a004120032002280001360142200320022900053701462003200228000d36014e200320022f00113b0152200320022d00133a0054200341003b0055200341c0006a4117200310868080800020034120200341206a10878080800020002003290320370300200020032903283703082000200329033037031020002003290338370318200341e0006a2480808080000bb00706017f057e017f017e047f027e2380808080004180016b2202248080808000200241086a41106a410036020042002103200241086a41086a420037030020024200370308200241086a108580808000200241f7013a0040200220022d00083a0041200220022800093601422002200229000d3701462002200228001536014e200220022f00193b0152200220022d001b3a0054200241003b0055200241c0006a4117200241206a108680808000200241206a4120200241e0006a10878080800020022903782001290318427f857c200229037022042001290310427f857c2205200454200229036822042001290308427f857c2206200454200229036022072001290300427f857c2204200754220820062008ad7c2206507172220820052008ad7c2205507172ad7c2107200442017c210902402004427f520d0042002103200642017c22064200520d00200542017c220550ad2103420021060b200241e0006a41186a2208200720037c370300200241e0006a41106a220a20053703002002200637036820022009370360200241d5006a220b41003b0000200241d2006a220c200241196a2f00003b0100200241d4006a220d2002411b6a2d00003a0000200241f7013a0040200220022d00083a0041200220022800093601422002200229000d3701462002200241156a28000036014e200241c0006a4117200241206a108680808000200241206a4120200241e0006a108880808000200c20002f00113b0100200d20002d00133a0000200241f7013a0040200220002d00003a004120022000280001360142200220002900053701462002200028000d36014e200b41003b0000200241c0006a4117200241206a108680808000200241206a4120200241e0006a10878080800020082903002103200a290300210520022903682107200141086a2903002104200141186a2903002109200141106a290300210620022001290300220e20022903607c220f3703602002200420077c2207200f200e542201ad7c220e370368200a200620057c220520072004542001200e5071722201ad7c22043703002008200920037c200520065420012004507172ad7c370300200b41003b0000200c20002f00113b0100200d20002d00133a0000200241f7013a0040200220002d00003a004120022000280001360142200220002900053701462002200028000d36014e200241c0006a4117200241206a108680808000200241206a4120200241e0006a108880808000200241013a0060200241e0006a410110848080800020024180016a2480808080000b0b0801004180080b01f700fc03046e616d6501f4030f000d6765745f63616c6c5f73697a65010f636f70795f63616c6c5f76616c7565020977726974655f6c6f67030b73797374656d5f68616c74040a7365745f72657475726e050a6765745f73656e646572061063727970746f5f6b656363616b323536070c6c6f61645f73746f72616765080c736176655f73746f7261676509115f5f7761736d5f63616c6c5f63746f72730a0573746172740b2e65726332305f6d696e74286c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e74323536290c7c766f6964206c61636861696e3a3a696e766f6b655f636f6e74726163745f6d6574686f643c766f69642c206c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e743235363e28766f696420282a29286c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e7432353629290d776c61636861696e3a3a75696e74323536206c61636861696e3a3a6c6f61645f66726f6d5f73746f726167653c6c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e743235363e28756e7369676e656420636861722c206c61636861696e3a3a6164647265737320636f6e737426290e3265726332305f7472616e73666572286c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e743235362900250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
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
            var input = ContractEncoder.Encode("totalSupply()");
            Console.WriteLine("ABI: " + input.ToHex());
            var status = virtualMachine.InvokeContract(contract, sender, input);
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
//            /* ERC-20: totalSupply (0x18160ddd) */
//            Console.WriteLine("\nERC-20: totalSupply()");
//            input = ContractEncoder.Encode("totalSupply()", sender);
//            Console.WriteLine("ABI: " + input.ToHex());
//            status = virtualMachine.InvokeContract(contract, sender, input);
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
            
//            /* ERC-20: balanceOf (0x70a08231) */
//            Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
//            input = ContractEncoder.Encode("balanceOf(address)", sender);
//            status = virtualMachine.InvokeContract(contract, sender, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
//            
//            /* ERC-20: transfer (0xa9059cbb) */
//            Console.WriteLine($"\nERC-20: transfer({to.Buffer.ToHex()}, {valueToTransfer})");
//            input = ContractEncoder.Encode("transfer(address,uint256)", to, valueToTransfer);
//            status = virtualMachine.InvokeContract(contract, sender, input); 
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
//            
//            /* ERC-20: balanceOf (0x70a08231) */
//            Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
//            input = ContractEncoder.Encode("balanceOf(address)", sender);
//            status = virtualMachine.InvokeContract(contract, sender, input);
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }
            
//            /* ERC-20: balanceOf (0x70a08231) */
//            Console.WriteLine($"\nERC-20: balanceOf({sender.Buffer.ToHex()})");
//            input = ContractEncoder.Encode("balanceOf(address)", sender);
//            status = virtualMachine.InvokeContract(contract, sender, input);
//            if (status != ExecutionStatus.Ok)
//            {
//                stateManager.Rollback();
//                Console.WriteLine("Contract execution failed: " + status);
//                goto exit_mark;
//            }

            /* ERC-20: balanceOf (0x40c10f19) */
            Console.WriteLine($"\nERC-20: mint({sender.Buffer.ToHex()},{Money.FromDecimal(1)})");
            input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(1));
            Console.WriteLine("ABI: " + input.ToHex());
            status = virtualMachine.InvokeContract(contract, sender, input); 
            if (status != ExecutionStatus.Ok)
            {
                stateManager.Rollback();
                Console.WriteLine("Contract execution failed: " + status);
                goto exit_mark;
            }
            
            /* ERC-20: totalSupply (0x18160ddd) */
            Console.WriteLine("\nERC-20: totalSupply()");
            Console.WriteLine("ABI: " + input.ToHex());
            input = ContractEncoder.Encode("totalSupply()");
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
            Console.WriteLine("ABI: " + input.ToHex());
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