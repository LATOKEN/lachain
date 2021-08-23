using System;
using System.IO;
using System.Numerics;
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
    public class StakingContractTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private IContainer? _container;
        
        private IStateManager _stateManager = null!;
        private IContractRegisterer _contractRegisterer = null!;

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
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        [Test]
        public void Test_OneNodeCycle()
        {
            var tx = new TransactionReceipt();

            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] publicKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            var sender = keyPair.PublicKey.GetAddress();
            
            var context = new InvocationContext(sender, _stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);

            var stakeAmount = BigInteger.Pow(10, 21);

            // Set balance for the staker
            {
                context.Snapshot.Balances.SetBalance(sender, Money.Parse("1000"));
                Assert.AreEqual(Money.Parse("1000"),context.Snapshot.Balances.GetBalance(sender));
            }
            
            // Become staker
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodBecomeStaker, publicKey, stakeAmount.ToUInt256());
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.BecomeStaker(publicKey, stakeAmount.ToUInt256(), frame));
            }
            
            // Get stake
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, sender);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStake(sender, frame));
                Assert.AreEqual(Money.Parse("1000"), frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // Able to validator
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodIsAbleToBeValidator, sender);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsAbleToBeValidator(sender, frame));
                Assert.AreEqual(1, BitConverter.ToInt32(frame.ReturnValue, 0));
            }
            
            // Withdraw stake
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, sender);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);

                context.Receipt.Block = 50;
                var cycle = (int) (50 / 20);
                contract.SetWithdrawRequestCycle(sender, cycle);

                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                
                Assert.AreEqual(ExecutionStatus.Ok, contract.WithdrawStake(publicKey, frame));
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStake(sender, frame));
                Assert.AreEqual(Money.Parse("0"), frame.ReturnValue.ToUInt256().ToMoney());
            }
        }

        [Test]
        public void Test_DelegateStaker()
        {
            var tx = new TransactionReceipt();

            var stakerKeyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] stakerPublicKey = CryptoUtils.EncodeCompressed(stakerKeyPair.PublicKey);
            var staker = stakerKeyPair.PublicKey.GetAddress();

            var validatorKeyPair = new EcdsaKeyPair(
                "0xE83385AF76B2B1997326B567461FB73DD9C27EAB9E1E86D26779F4650C5F2B75".HexToBytes()
                    .ToPrivateKey());
            byte[] validatorPublicKey = CryptoUtils.EncodeCompressed(validatorKeyPair.PublicKey);
            var validator = stakerKeyPair.PublicKey.GetAddress();

            var context = new InvocationContext(staker, _stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);

            var stakeAmount = BigInteger.Pow(10, 21);

            // Set balance for the staker
            {
                context.Snapshot.Balances.SetBalance(staker, Money.Parse("1000"));
                Assert.AreEqual(Money.Parse("1000"), context.Snapshot.Balances.GetBalance(staker));
            }

            // Become staker
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodBecomeStaker, validatorPublicKey,
                    stakeAmount.ToUInt256());
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok,
                    contract.BecomeStaker(validatorPublicKey, stakeAmount.ToUInt256(), frame));
            }

            // Get stake
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, staker);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStake(staker, frame));
                Assert.AreEqual(Money.Parse("1000"), frame.ReturnValue.ToUInt256().ToMoney());
            }

            // Withdraw stake by validator
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, staker);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.ExecutionHalted, contract.WithdrawStake(validatorPublicKey, frame));
            }
            
            // Able to validator
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodIsAbleToBeValidator, staker);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsAbleToBeValidator(validator, frame));
                Assert.AreEqual(1, BitConverter.ToInt32(frame.ReturnValue, 0));
            }
            
            // Withdraw stake by staker
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, staker);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);

                context.Receipt.Block = 50;
                var cycle = (int) (50 / 20);
                contract.SetWithdrawRequestCycle(staker, cycle);

                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                
                Assert.AreEqual(ExecutionStatus.Ok, contract.WithdrawStake(stakerPublicKey, frame));
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStake(staker, frame));
                Assert.AreEqual(Money.Parse("0"), frame.ReturnValue.ToUInt256().ToMoney());
            }
        }
    }
}