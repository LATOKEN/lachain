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
        private IContainer? _container;

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
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
         //   _container.Dispose();
        }

        [Test]
        public void Test_VirtualMachine_InvokeContract()
        {
            var stateManager = _container?.Resolve<IStateManager>();
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
        
        [Test]
        public void Test_VirtualMachine_InvokeCallingContract()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceA = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.A.wasm");
            var resourceB = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.B.wasm");
            if(resourceA is null)
                Assert.Fail("Failed t read script from resources");
            var aCode = new byte[resourceA!.Length];
            resourceA!.Read(aCode, 0, (int)resourceA!.Length);
            if(resourceB is null)
                Assert.Fail("Failed t read script from resources");
            var bCode = new byte[resourceB!.Length];
            resourceB!.Read(bCode, 0, (int)resourceB!.Length);
            var stateManager = _container.Resolve<IStateManager>();
            // A
            var aAddress = UInt160Utils.Zero;
            var aContract = new Contract
            (
                aAddress,
                aCode
            );
            if (!VirtualMachine.VerifyContract(aContract.ByteCode))
                throw new Exception("Unable to validate smart-contract code");
            // B
            var bAddress = "0xfd893ce89186fc6861d339cb6ab5d75458e3daf3".HexToBytes().ToUInt160();
            var bContract = new Contract
            (
                bAddress,
                bCode
            );
            if (!VirtualMachine.VerifyContract(bContract.ByteCode))
                throw new Exception("Unable to validate smart-contract code");
            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, aContract);
            snapshot.Contracts.AddContract(UInt160Utils.Zero, bContract);
            stateManager.Approve();
            for (var i = 0; i < 1; ++i)
            {
                var currentTime = TimeUtils.CurrentTimeMillis();
                var currentSnapshot = stateManager.NewSnapshot();
                var sender = UInt160Utils.Zero;
                currentSnapshot.Balances.AddBalance(UInt160Utils.Zero, 100.ToUInt256().ToMoney());
                var transactionReceipt = new TransactionReceipt();
                transactionReceipt.Transaction = new Transaction();
                transactionReceipt.Transaction.Value = 0.ToUInt256();
                var context = new InvocationContext(sender, currentSnapshot, transactionReceipt);
                {
                    Console.WriteLine($"\nA: init({bAddress.ToHex()})");
                    var input = ContractEncoder.Encode("init(address)", bAddress);
                    Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(aContract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        Console.WriteLine($"Result: {status.ReturnValue?.ToHex()}");
                        goto exit_mark;
                    }
                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    transactionReceipt.Transaction.Value = 0.ToUInt256();
                    context = new InvocationContext(sender, context.Snapshot, transactionReceipt);
                    Console.WriteLine("\nA: getA()");
                    var input = ContractEncoder.Encode("getA()");
                    Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(aContract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        Console.WriteLine($"Result: {status.ReturnValue?.ToHex()}");
                        goto exit_mark;
                    }
                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                stateManager.Approve();
            exit_mark:
                var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
                Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
            }
        }
    }
}