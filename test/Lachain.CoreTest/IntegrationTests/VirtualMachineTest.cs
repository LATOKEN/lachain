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
        private IContainer _container;

        [SetUp]
        public void Setup()
        {
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
            _container.Dispose();

            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_VirtualMachine_InvokeContract()
        {
            var stateManager = _container.Resolve<IStateManager>();

            var hash = UInt160Utils.Zero;
            var contract = new Contract
            (
                hash,
                "0061736D01000000011C0660017F006000017F60037F7F7F0060027F7F0060000060017F017F0290010703656E76156765745F7472616E736665727265645F66756E6473000003656E760D6765745F63616C6C5F73697A65000103656E760F636F70795F63616C6C5F76616C7565000203656E760A7365745F72657475726E000303656E760B73797374656D5F68616C74000003656E760C6C6F61645F73746F72616765000303656E760C736176655F73746F7261676500030307060304050202040405017001010105030100020608017F01418080040B071202066D656D6F72790200057374617274000C0A9E06061C00034020004200370300200041086A21002001417F6A22010D000B0B2E004100410036028080044100410036028480044100410036028C800441003F0041107441F0FF7B6A36028880040BA60101047F418080042101024003400240200128020C0D002001280208220220004F0D020B200128020022010D000B41002101410028020821020B02402002200041076A41787122036B22024118490D00200120036A41106A22002001280200220436020002402004450D00200420003602040B2000200241706A3602082000410036020C2000200136020420012000360200200120033602080B2001410136020C200141106A0B2D002000411F6A21000340200120002D00003A0000200141016A21012000417F6A21002002417F6A22020D000B0B2D002001411F6A21010340200120002D00003A00002001417F6A2101200041016A21002002417F6A22020D000B0BCB0301047F230041A0016B22002400200041086A100002400240024002402000290308200041106A290300844200520D00100841001001220136020441002001100922023602084100200120021002024002400240200141034D0D00410020022802002203360200200341DDBBC98A01460D01200341ED9899E703460D020B41004100100341011004000B2001417C6A4120490D02200241046A200041186A4104100A2000280218210120004180016A41186A420037030020004200370390012000420037038801200042003703800120004180016A200041E0006A1005200041C0006A41186A420037030020004200370350200042003703482000420037034020002802602102200041206A4104100720002002200141E4006C6A360220200041C0006A200041206A100641010D0341004100100341011004000B20004198016A420037030020004200370390012000420037038801200042003703800120004180016A200041E0006A10052000200028026036021C4100450D0341004100100341011004000B41004100100341011004000B200041A0016A240041030F0B41004100100341001004000B412010092201410410072000411C6A20014104100B20014120100341001004000B00740970726F647563657273010C70726F6365737365642D62790105636C616E675431302E302E3120286769743A2F2F6769746875622E636F6D2F6C6C766D2F6C6C766D2D70726F6A65637420623661313733343336373838653638333239636335653965653066363531623630336136333765332900B701046E616D6501AF010D00156765745F7472616E736665727265645F66756E6473010D6765745F63616C6C5F73697A65020F636F70795F63616C6C5F76616C7565030A7365745F72657475726E040B73797374656D5F68616C74050C6C6F61645F73746F72616765060C736176655F73746F7261676507085F5F627A65726F38080B5F5F696E69745F6865617009085F5F6D616C6C6F630A0B5F5F62653332746F6C654E0B0B5F5F6C654E746F626533320C057374617274"
                    .HexToBytes()
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

                /* give to sender 1 token */
                var valueToTransfer = Money.Wei;
                stateManager.CurrentSnapshot.Storage.SetValue(contract.ContractAddress, sender.ToUInt256(),
                    (valueToTransfer * 3).ToUInt256());

                var transactionReceipt = new TransactionReceipt();
                transactionReceipt.Transaction = new Transaction();
                transactionReceipt.Transaction.Value = 0.ToUInt256();
                var context = new InvocationContext(sender, currentSnapshot, transactionReceipt);

                {
                    /* ERC-20: totalSupply (0x18160ddd) */
                    Console.WriteLine("\nERC-20: totalSupply()");
                    var input = ContractEncoder.Encode("totalSupply()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        return;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* mint */
                    Console.WriteLine($"\nERC-20: mint({sender.ToHex()},{Money.FromDecimal(100)})");
                    var input = ContractEncoder.Encode("mint(address,uint256)", sender, Money.FromDecimal(100));
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: totalSupply (0x18160ddd) */
                    Console.WriteLine("\nERC-20: totalSupply()");
                    var input = ContractEncoder.Encode("totalSupply()");
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: balanceOf */
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: transfer */
                    Console.WriteLine($"\nERC-20: transfer({to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("transfer(address,uint256)", to, Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: balanceOf */
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: balanceOf */
                    Console.WriteLine($"\nERC-20: balanceOf({to.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", to);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: approve */
                    Console.WriteLine($"\nERC-20: approve({to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("approve(address,uint256)", to, Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: transferFrom */
                    Console.WriteLine($"\nERC-20: transferFrom({to.ToHex()},{Money.FromDecimal(50)})");
                    var input = ContractEncoder.Encode("transferFrom(address,address,uint256)", sender, to,
                        Money.FromDecimal(50));
                    // Console.WriteLine($"ABI: {input.ToHex()}");
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }

                {
                    /* ERC-20: balanceOf */
                    Console.WriteLine($"\nERC-20: balanceOf({sender.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", sender);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
                        goto exit_mark;
                    }

                    Console.WriteLine($"Result: {status.ReturnValue!.ToHex()}");
                }
                {
                    /* ERC-20: balanceOf */
                    Console.WriteLine($"\nERC-20: balanceOf({to.ToHex()}");
                    var input = ContractEncoder.Encode("balanceOf(address)", to);
                    // Console.WriteLine("ABI: " + input.ToHex());
                    var status = VirtualMachine.InvokeWasmContract(contract, context, input, 100_000_000_000_000UL);
                    if (status.Status != ExecutionStatus.Ok)
                    {
                        stateManager.Rollback();
                        Console.WriteLine("Contract execution failed: " + status.Status);
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