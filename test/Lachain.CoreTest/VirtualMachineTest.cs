using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using NUnit.Framework;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;

namespace Lachain.CoreTest
{
    public class VirtualMachineTest
    {
        private readonly IContainer _container;

        public VirtualMachineTest()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        [SetUp]
        public void Setup()
        {
            TestUtils.DeleteTestChainData();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container.Dispose();
        }

        [Test]
        public void Test_VirtualMachine_InvokeContract()
        {
            // TODO: rewrite this test
            // var virtualMachine = _container.Resolve<IVirtualMachine>();
            //
            // var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
            // var input = "0xffffffffffffffff".HexToBytes();
            //
            // var byteCode =
            //     "0061736d010000000117056000017f60037f7f7f0060027f7f0060000060017f0002360303656e760d636f707963616c6c76616c7565000103656e760b67657463616c6c73697a65000003656e760877726974656c6f6700020304030304040404017000000503010001071202066d656d6f7279020005737461727400030a59034301017f4100410028020441106b2200360204410010014110100020004200370308200041086a1004410020002903083703104110410810024100200041106a3602040b0600200010050b0c00200041002903103703000b"
            //         .HexToBytes();
            // var contract = new Contract
            // {
            //     ContractAddress = UInt160Utils.Zero,
            //     ByteCode = ByteString.CopyFrom(byteCode)
            // };

            // var status = virtualMachine.InvokeWasmContract(contract, new InvocationContext(sender), input, 100_000_000);
            // Console.WriteLine("Contract executed with status: " + status);
        }
    }
}