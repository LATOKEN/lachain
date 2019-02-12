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
                Wasm = ByteString.CopyFrom("0061736d010000000117056000017f60037f7f7f0060027f7f0060017f006000000299010803656e760d6765745f63616c6c5f73697a65000003656e760f636f70795f63616c6c5f76616c7565000103656e760977726974655f6c6f67000203656e760b73797374656d5f68616c74000303656e760a7365745f72657475726e000203656e760a6765745f73656e646572000303656e761063727970746f5f6b656363616b323536000103656e760c6c6f61645f73746f7261676500010304030404010405017001010105030100020615037f01419088040b7f00419088040b7f004182080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e64030205737461727400090ace0a0302000b930902027f037e23808080800041b0016b220024808080800002400240024002400240024002400240024010808080800041034b0d002000410036020c0c010b41004104200041106a10818080800020002000280210220136020c2001450d002000410c6a4104108280808000200028020c220141a2f0caeb7d4c0d01200141efc08a8c034a0d02200141a3f0caeb7d460d03200141c082bfc801470d07024010808080800041374b0d0041061083808080000b41044138200041106a108180808000200029031021022000290318210320002000280220360268200020033703602000200237035820004190016a41002d008188808000200041d8006a108a8080800020004180016a4100360200200041f8006a42003703002000420037037020004190016a41002d008088808000200041f0006a108a80808000200041b0016a2480808080000f0b4105108380808000200041b0016a2480808080000f0b20014189bc9d9d7b460d02200141a98bf0dc7b460d0320014198acb4e87d470d05024010808080800041034b0d0041061083808080000b41044104200041f0006a108180808000200041a0016a410036020020004198016a42003703002000420037039001200041106a41002d00808880800020004190016a108a8080800020002903102102200029031821032000290320210420002000290328370328200020043703202000200337031820002002370310200041106a4120108480808000200041b0016a2480808080000f0b200141f0c08a8c03460d03200141ddc5b5f703470d04024010808080800041034b0d0041061083808080000b41044104200041f0006a1081808080004105108380808000200041b0016a2480808080000f0b024010808080800041cb004b0d0041061083808080000b410441cc00200041106a1081808080004105108380808000200041b0016a2480808080000f0b024010808080800041034b0d0041061083808080000b41044104200041f0006a1081808080004105108380808000200041b0016a2480808080000f0b024010808080800041374b0d0041061083808080000b41044138200041106a108180808000200029031021022000290318210320002000280220360268200020033703602000200237035820004180016a4100360200200041f8006a420037030020004200370370200041f0006a10858080800020004190016a41002d008188808000200041f0006a108a8080800020004190016a41002d008188808000200041d8006a108a80808000200041013a00900120004190016a4101108480808000200041b0016a2480808080000f0b024010808080800041174b0d0041061083808080000b41044118200041f0006a108180808000200029037021022000290378210320002000280280013602a00120002003370398012000200237039001200041106a41002d00818880800020004190016a108a8080800020002903102102200029031821032000290320210420002000290328370328200020043703202000200337031820002002370310200041106a4120108480808000200041b0016a2480808080000f0b4105108380808000200041b0016a2480808080000bb30101017f23808080800041e0006b2203248080808000200320013a0040200320022d00003a004120032002280001360142200320022900053701462003200228000d36014e200320022f00113b0152200320022d00133a0054200341003b0055200341c0006a4117200310868080800020034120200341206a10878080800020002003290320370300200020032903283703082000200329033037031020002003290338370318200341e0006a2480808080000b0b0901004180080b0275f7008c02046e616d650184020b000d6765745f63616c6c5f73697a65010f636f70795f63616c6c5f76616c7565020977726974655f6c6f67030b73797374656d5f68616c74040a7365745f72657475726e050a6765745f73656e646572061063727970746f5f6b656363616b323536070c6c6f61645f73746f7261676508115f5f7761736d5f63616c6c5f63746f7273090573746172740a776c61636861696e3a3a75696e74323536206c61636861696e3a3a6c6f61645f66726f6d5f73746f726167653c6c61636861696e3a3a616464726573732c206c61636861696e3a3a75696e743235363e28756e7369676e656420636861722c206c61636861696e3a3a6164647265737320636f6e7374262900250970726f647563657273010c70726f6365737365642d62790105636c616e6705392e302e30".HexToBytes())
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
            
            /* ERC-20: totalSupply (0x18160ddd) */
            Console.WriteLine("\nERC-20: totalSupply()");
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