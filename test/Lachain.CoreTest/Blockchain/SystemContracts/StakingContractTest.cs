using System;
using System.IO;
using System.Numerics;
using System.Reflection;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Hardfork;
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
using Lachain.Networking;
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
        private IConfigManager _configManager = null!;
        private IBlockManager _blockManager = null!;

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
            _blockManager = _container.Resolve<IBlockManager>();
            _configManager = _container.Resolve<IConfigManager>();
            // set chainId from config
            if (TransactionUtils.ChainId(false) == 0)
            {
                var chainId = _configManager.GetConfig<NetworkConfig>("network")?.ChainId;
                var newChainId = _configManager.GetConfig<NetworkConfig>("network")?.NewChainId;
                TransactionUtils.SetChainId((int)chainId!, (int)newChainId!);
                HardforkHeights.SetHardforkHeights(_configManager.GetConfig<HardforkConfig>("hardfork") ?? throw new InvalidOperationException());
                StakingContract.Initialize(_configManager.GetConfig<NetworkConfig>("network")!);
            }
        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void Test_OneNodeCycle()
        {
            _blockManager.TryBuildGenesisBlock();

            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            var tx = TestUtils.GetRandomTransactionFromAddress(keyPair, 0, HardforkHeights.IsHardfork_9Active(0));
            byte[] publicKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            var sender = keyPair.PublicKey.GetAddress();
            
            var context = new InvocationContext(sender, _stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);

            var stakeAmount = BigInteger.Pow(10, 21);

            // Set balance for the staker
            {
                context.Snapshot.Balances.MintLaToken(sender, Money.Parse("1000"));
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
                Assert.AreEqual(1.ToUInt256(), frame.ReturnValue.ToUInt256());
            }

            var validatorsInfo = _configManager.GetConfig<GenesisConfig>("genesis")!.Validators;
            var totalStake = Money.Parse("1000");

            foreach (var info in validatorsInfo)
            {
                totalStake += Money.Parse(info.StakeAmount!);
            }

            // Get total active stake
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetTotalActiveStake);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetTotalActiveStake(frame));
                Assert.AreEqual(totalStake, frame.ReturnValue.ToUInt256().ToMoney());
            }

            // Get total stake for staker
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStakerTotalStake, sender);
                var call = _contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStakerTotalStake(sender, frame));
                Assert.AreEqual(totalStake, frame.ReturnValue.ToUInt256().ToMoney());
            }
        }
    }
}