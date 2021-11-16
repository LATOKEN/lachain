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
using Org.BouncyCastle.Math;

namespace Lachain.CoreTest.IntegrationTests
{
    [Ignore("Need top recompile contracts")]
    public class LARC20CompilationTest
    {
        private IContainer? _container;

        public LARC20CompilationTest()
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
        }
        
        [Test]
        public void Test_VirtualMachine_InvokeERC20Contract()
        {
            var stateManager = _container!.Resolve<IStateManager>();

            var assembly = Assembly.GetExecutingAssembly();
            var resourceERC20 = assembly.GetManifestResourceStream("Lachain.CoreTest.Resources.scripts.ERC20.wasm");
            if(resourceERC20 is null)
                Assert.Fail("Failed to read bytecode from resources");
            var byteCode = new byte[resourceERC20!.Length];
            resourceERC20!.Read(byteCode, 0, (int)resourceERC20!.Length);
            
            var hash = UInt160Utils.Zero;
            var contract = new Contract(
                hash, byteCode
            );
            if (!VirtualMachine.VerifyContract(contract.ByteCode))
                throw new Exception("Unable to validate smart-contract code");

            var snapshot = stateManager.NewSnapshot();
            snapshot.Contracts.AddContract(UInt160Utils.Zero, contract);
            stateManager.Approve();

            Console.WriteLine("Contract Hash: " + hash.ToHex());

            for (var i = 0; i < 1; ++i)
            {
                var currentTime = TimeUtils.CurrentTimeMillis();
                var currentSnapshot = stateManager.NewSnapshot();

                var sender = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes().ToUInt160();
                var to = "0xfd893ce89186fc6861d339cb6ab5d75458e3daf3".HexToBytes().ToUInt160();

                var transactionReceipt = new TransactionReceipt();
                transactionReceipt.Transaction = new Transaction();
                transactionReceipt.Transaction.Value = 0.ToUInt256();
                var context = new InvocationContext(sender, currentSnapshot, transactionReceipt);

                {
                    // ERC-20: name #1#
                    Console.WriteLine("\nERC-20: name()");
                    var input = ContractEncoder.Encode("name()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {"LAtoken"}, {System.Text.Encoding.Default.GetString(status.ReturnValue!)}");
                }
                {
                    // ERC-20: symbol #1#
                    Console.WriteLine("\nERC-20: symbol()");
                    var input = ContractEncoder.Encode("symbol()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {"LA"}, {System.Text.Encoding.Default.GetString(status.ReturnValue!)}");
                }
                {
                    // ERC-20: decimals #1#
                    Console.WriteLine("\nERC-20: decimals()");
                    var input = ContractEncoder.Encode("decimals()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {18}, {new BigInteger(status.ReturnValue!)}");
                }
                {
                    // ERC-20: totalSupply #1#
                    Console.WriteLine("\nERC-20: totalSupply()");
                    var input = ContractEncoder.Encode("totalSupply()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {0}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // mint #1#
                    Console.WriteLine($"\nERC-20: mint({sender.ToHex()},{Money.FromDecimal(100)})");
                    var input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(100));
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: totalSupply #1#
                    Console.WriteLine("\nERC-20: totalSupply()");
                    var input = ContractEncoder.Encode("totalSupply()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {100}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {100}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: transfer #1#
                    Console.WriteLine($"\nERC-20: transfer({to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("transfer(address,uint256)", to, Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {50}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({to.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", to);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {50}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: approve #1#
                    Console.WriteLine($"\nERC-20: approve({to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("approve(address,uint256)", to, Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: allowance #1#
                    Console.WriteLine($"\nERC-20: allowance({sender.ToHex()},{to.ToHex()})");
                    var input = ContractEncoder.Encode("allowance(address,address)", sender, to);
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {50}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: increaseAllowance #1#
                    Console.WriteLine($"\nERC-20: increaseAllowance({to.ToHex()},{Money.FromDecimal(10)})");
                    var input = ContractEncoder.Encode("increaseAllowance(address,uint256)", to, Money.FromDecimal(10));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: allowance #1#
                    Console.WriteLine($"\nERC-20: allowance({sender.ToHex()},{to.ToHex()})");
                    var input = ContractEncoder.Encode("allowance(address,address)", sender, to);
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {60}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: decreaseAllowance #1#
                    Console.WriteLine($"\nERC-20: decreaseAllowance({to.ToHex()},{Money.FromDecimal(10)})");
                    var input = ContractEncoder.Encode("decreaseAllowance(address,uint256)", to, Money.FromDecimal(10));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: allowance #1#
                    Console.WriteLine($"\nERC-20: allowance({sender.ToHex()},{to.ToHex()})");
                    var input = ContractEncoder.Encode("allowance(address,address)", sender, to);
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {50}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: transferFrom #1#
                    Console.WriteLine($"\nERC-20: transferFrom({sender.ToHex()},{to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("transferFrom(address,address,uint256)", sender, to,
                        Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");

                    // change sender
                    context = new InvocationContext(to, context.Snapshot, transactionReceipt);

                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {0}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({to.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", to);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {100}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // burn #1#
                    Console.WriteLine($"\nERC-20: burn({to.ToHex()},{Money.FromDecimal(30)})");
                    var input = ContractEncoder.Encode("burn(address,uint256)", to, Money.FromDecimal(30));
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {true}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: totalSupply #1#
                    Console.WriteLine("\nERC-20: totalSupply()");
                    var input = ContractEncoder.Encode("totalSupply()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {70}, {status.ReturnValue!.ToHex()}");
                }
                {
                    // ERC-20: balanceOf #1#
                    Console.WriteLine($"\nERC-20: balanceOf({to.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", to);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    Assert.AreEqual(status.Status, ExecutionStatus.Ok, $"Contract is halted, result: {status.ReturnValue!.ToHex()}");
                    Console.WriteLine($"Result: {70}, {status.ReturnValue!.ToHex()}");
                }

                stateManager.Approve();
                var elapsedTime = TimeUtils.CurrentTimeMillis() - currentTime;
                Console.WriteLine("Elapsed Time: " + elapsedTime + "ms");
            }
        }
    }
}