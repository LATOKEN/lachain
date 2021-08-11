using System;
using System.IO;
using System.Reflection;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.SystemContracts
{
    public class NativeTokenContractTest
    {
        private IContainer? _container;

        private IStateManager _stateManager = null!;
        private IContractRegisterer _contractRegisterer = null!;

        private EcdsaKeyPair _minterKeyPair = null!;
        private byte[] _minterPubKey = null!;
        private UInt160 _minterAdd = null!;
        
        private EcdsaKeyPair _mintCntrlKeyPair = null!;
        private byte[] _mintCntrlPubKey = null!;
        private UInt160 _mintCntrlAdd = null!;


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

            _stateManager = _container.Resolve<IStateManager>();
            _contractRegisterer = _container.Resolve<IContractRegisterer>();

            _minterKeyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            _minterPubKey = CryptoUtils.EncodeCompressed(_minterKeyPair.PublicKey);
            _minterAdd = _minterKeyPair.PublicKey.GetAddress();

            _mintCntrlKeyPair = new EcdsaKeyPair("0xE83385AF76B2B1997326B567461FB73DD9C27EAB9E1E86D26779F4650C5F2B75".HexToBytes()
                .ToPrivateKey());
            _mintCntrlPubKey = CryptoUtils.EncodeCompressed(_mintCntrlKeyPair.PublicKey);
            _mintCntrlAdd = _mintCntrlKeyPair.PublicKey.GetAddress();
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_NativeTokenMinting()
        {
            var tx = new TransactionReceipt();

            var context = new InvocationContext(_mintCntrlAdd, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);

            var keyPair = new EcdsaKeyPair("0x4433d156e8c53bf5b50af07aa95a29436f29a94e0ccc5d58df8e57bdc8583c32"
                .HexToBytes().ToPrivateKey());
            var address = keyPair.PublicKey.GetAddress();

            // set the wallet to mint the tokens
            {
                context.Snapshot.Balances.SetBalance(address, Money.Parse("1000"));
                
                var input = ContractEncoder.Encode(Lrc20Interface.MethodBalanceOf, address);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.BalanceOf(address, frame));
                Assert.AreEqual(Money.Parse("1000"), frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // check the allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodGetAllowedSupply);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetAllowedSupply(frame));
                Assert.AreEqual(Money.Parse("0"), frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // set the allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetAllowedSupply, Money.Parse("10000"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetAllowedSupply(Money.Parse("10000"), frame));
                Assert.AreEqual(Money.Parse("10000"), frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // set minter
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetMinter, _minterAdd);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetMinter(_minterAdd, frame));
                Assert.AreEqual(_minterAdd, frame.ReturnValue.ToUInt160());
            }
            
            // mint tokens to address
            {
                context.Sender = context.Snapshot.Balances.GetMinter();
                
                var input = ContractEncoder.Encode(Lrc20Interface.MethodMint, address, Money.Parse("100"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.Mint(address, Money.Parse("100"), frame));
                Assert.AreEqual(Money.Parse("1100"), frame.ReturnValue.ToUInt256().ToMoney());
            }
        }

        [Test]
        public void Test_InvalidMintController()
        {
            var tx = new TransactionReceipt();
            var context = new InvocationContext(_mintCntrlAdd, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);
            
            // set minter
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetMinter, _minterAdd);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetMinter(_minterAdd, frame));
                Assert.AreEqual(_minterAdd, frame.ReturnValue.ToUInt160());
            }
            
            
            // set the allowedSupply
            {
                context = new InvocationContext(_stateManager.LastApprovedSnapshot.Balances.GetMinter(), _stateManager.LastApprovedSnapshot, tx);
                contract = new NativeTokenContract(context);
                
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetAllowedSupply, Money.Parse("10000"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.SetAllowedSupply(Money.Parse("10000"), frame));
            }
            
            // verify allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodGetAllowedSupply);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetAllowedSupply(frame));
                Assert.AreEqual(Money.Parse("0"), frame.ReturnValue.ToUInt256().ToMoney());
            }
        }
        
        [Test]
        public void Test_InvalidMinter()
        {
            var tx = new TransactionReceipt();

            var context = new InvocationContext(_mintCntrlAdd, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);
            
            var keyPair = new EcdsaKeyPair("0x4433d156e8c53bf5b50af07aa95a29436f29a94e0ccc5d58df8e57bdc8583c32"
                .HexToBytes().ToPrivateKey());
            var address = keyPair.PublicKey.GetAddress();
            
            // set the allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetAllowedSupply, Money.Parse("10000"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetAllowedSupply(Money.Parse("10000"), frame));
            }
            
            // mint tokens to address
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodMint, address, Money.Parse("100"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.Mint(address, Money.Parse("100"), frame));
                Assert.AreEqual(Money.Parse("0"), context.Snapshot.Balances.GetBalance(address));
            }
        }
        
        [Test]
        public void Test_MaxSupply()
        {
            var tx = new TransactionReceipt();

            var context = new InvocationContext(_mintCntrlAdd, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);
            
            var keyPair = new EcdsaKeyPair("0x4433d156e8c53bf5b50af07aa95a29436f29a94e0ccc5d58df8e57bdc8583c32"
                .HexToBytes().ToPrivateKey());
            var address = keyPair.PublicKey.GetAddress();
            
            // set the allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetAllowedSupply, Money.Parse("10000"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetAllowedSupply(Money.Parse("10000"), frame));
            }
            
            // set the allowedSupply > maxLimit
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetAllowedSupply, Money.Parse("1000000001"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.SetAllowedSupply(Money.Parse("1000000001"), frame));
            }
            
            // verify allowedSupply
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodGetAllowedSupply);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetAllowedSupply(frame));
                Assert.AreEqual(Money.Parse("10000"), frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // mint tokens to address
            {
                context = new InvocationContext(_stateManager.LastApprovedSnapshot.Balances.GetMinter(), _stateManager.LastApprovedSnapshot, tx);
                contract = new NativeTokenContract(context);
                
                var input = ContractEncoder.Encode(Lrc20Interface.MethodMint, address, Money.Parse("1000000000"));
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.Mint(address, Money.Parse("1000000000"), frame));
            }
        }

        [Test]
        public void Test_SetMinter()
        {
            var tx = new TransactionReceipt();

            var context = new InvocationContext(_mintCntrlAdd, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);

            // set the minter
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetMinter, _minterAdd);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SetMinter(_minterAdd, frame));
                Assert.AreEqual(_minterAdd, frame.ReturnValue.ToUInt160());
            }
            
            // get the minter
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodGetMinter);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetMinter(frame));
                Assert.AreEqual(_minterAdd, frame.ReturnValue.ToUInt160());
            }
        }
        
        [Test]
        public void Test_SetMinterInvalidMintCtlr()
        {
            var keyPair = new EcdsaKeyPair("0x4433d156e8c53bf5b50af07aa95a29436f29a94e0ccc5d58df8e57bdc8583c32"
                .HexToBytes().ToPrivateKey());
            var address = keyPair.PublicKey.GetAddress();
            
            var tx = new TransactionReceipt();

            var context = new InvocationContext(address, _stateManager.LastApprovedSnapshot, tx);
            var contract = new NativeTokenContract(context);
            
            // set the minter
            {
                var input = ContractEncoder.Encode(Lrc20Interface.MethodSetMinter, _minterAdd);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.NativeTokenContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.SetMinter(_minterAdd, frame));
            }
        }
    }
}