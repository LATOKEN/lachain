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
                Wasm = ByteString.CopyFrom("0061736d010000000131096000006000017f60037f7f7f0060027f7f0060017f0060017f017f60027f7e017f60027f7f017f60057f7e7e7e7e017f0283010703656e760d6765745f63616c6c5f73697a65000103656e760f636f70795f63616c6c5f76616c7565000203656e760977726974655f6c6f67000303656e760a7365745f72657475726e000303656e760c6c6f61645f73746f72616765000203656e760a6765745f73656e646572000403656e760c736176655f73746f726167650002031b1a00050000050004000000000003040504050607020302080707070405017001040405030100020615037f01418088040b7f00418088040b7f004180080b072d04066d656d6f727902000b5f5f686561705f6261736503010a5f5f646174615f656e6403020573746172740009090a010041000b040c0e10120ac20d1a02000b290020004200370300200041186a4200370300200041106a4200370300200041086a420037030020000be10101027f230041106b2200240002400240100041034b0d00100a0c010b410041042000410c6a100b2201100120014104100202400240024002400240200028020c220141dc9bd8c0014a0d00200141bbb996c87a460d01200141beda8beb7d460d02200141b3cffaca00470d054101100d200041106a24000f0b200141b184828507460d02200141dde5e19d02460d03200141dd9bd8c001470d04100e4102100d200041106a24000f0b100f200041106a24000f0b4103100d200041106a24000f0b1011200041106a24000f0b4104100d200041106a24000f0b200041106a24000b2a01017f230041106b22002400200041effdb6f57d36020c2000410c6a100b41041002200041106a24000b040020000b02000b2901017f230041106b2201240010001a410441042001410f6a100b10012000110000200141106a24000b2a01017f230041106b22002400200041baf9aef50136020c2000410c6a100b41041003200041106a24000b4501017f230041c0006b2200240010001a4104411c200041206a100b10012000200041206a36021c200041086a2000411c6a10132000411c6a10151016200041c0006a24000b02000b4301017f230041c0006b2200240010001a41044118200041206a100b10012000200041206a36021c200041086a2000411c6a1013200041086a1014200041c0006a24000b02000b1600200010172100200120012802002000101e3602000b2d01017f230041206b22012400200110081a2000100b41142001100b22001004200041201003200141206a24000b2e01017f230041106b22012400200020002802002001410c6a1020360200200128020c2100200141106a240020000b5a01047f230041e0006b22012400200141c8006a10171a200141c8006a100b22021005200141286a1008210320024114200141286a100b220410042003200141086a2000ad101810191a2002411420041006200141e0006a24000b1f0020004200370000200041106a4100360000200041086a420037000020000b26002000420037030820002001370300200041186a4200370300200041106a420037030020000b5801017f230041206b22022400200220002001101a200041186a200241186a290300370300200041106a200241106a290300370300200041086a200241086a29030037030020002002290300370300200241206a240020000b3e01017f230041e0006b22032400200341206a2002101b200341c0006a2001200341206a101c2000200341c0006a200342011018101c200341e0006a24000b3a002000100822002001290300427f8537030020002001290308427f8537030820002001290310427f8537031020002001290318427f853703180b800201047e20004200420042004200101d21002002290300220320012903007c2204200029030022057c21060240024020042003540d00200542005220065071450d010b2000200029030842017c3703080b200020063703002002290308220320012903087c2204200029030822057c21060240024020042003540d00200542005220065071450d010b2000200029031042017c3703100b200041086a20063703002002290310220320012903107c2204200029031022057c21060240024020042003540d00200542005220065071450d010b2000200029031842017c3703180b200041106a20063703002000200229031820012903187c20002903187c3703180b20002000200437031820002003370310200020023703082000200137030020000bf10201017f20002d0000210220014100101f20023a000020002d0001210220014101101f20023a000020002d0002210220014102101f20023a000020002d0003210220014103101f20023a000020002d0004210220014104101f20023a000020002d0005210220014105101f20023a000020002d0006210220014106101f20023a000020002d0007210220014107101f20023a000020002d0008210220014108101f20023a000020002d0009210220014109101f20023a000020002d000a21022001410a101f20023a000020002d000b21022001410b101f20023a000020002d000c21022001410c101f20023a000020002d000d21022001410d101f20023a000020002d000e21022001410e101f20023a000020002d000f21022001410f101f20023a000020002d0010210220014110101f20023a000020002d0011210220014111101f20023a000020002d0012210220014112101f20023a000020002d0013210220014113101f20023a0000200041146a0b0700200020016a0b110020012000280000360200200041046a0b".HexToBytes())
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