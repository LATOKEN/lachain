using System;
using System.IO;
using System.Reflection;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.IntegrationTests
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
            var stateManager = _container.Resolve<IStateManager>();
            var tx = new TransactionReceipt();
            
            var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
            var input = "0xffffffffffffffff".HexToBytes();
            
            var byteCode =
                "0061736d01000000018480808000016000000382808080000100048480808000017000000583808080000100010681808080000007928080800002066d656d6f7279020005737461727400000a8880808000018280808000000b"
                    .HexToBytes();
            var contract = new Contract(UInt160Utils.Zero, byteCode);

            var status = VirtualMachine.InvokeWasmContract(contract, new InvocationContext(sender, stateManager.LastApprovedSnapshot, tx), input, 100_000_000);
            Console.WriteLine("Contract executed with status: " + status);
        }
    }
}