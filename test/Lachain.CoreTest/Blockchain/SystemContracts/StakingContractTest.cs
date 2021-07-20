using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.SystemContracts.Utils;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Blockchain.VM.ExecutionFrame;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using LibVRF.Net;
using Nethereum.ABI.Util;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.SystemContracts

{
    public class StakingContractTest
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private readonly IContainer _container;

        public StakingContractTest()
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
        public void Test_OneNodeCycle()
        {
            var stateManager = _container.Resolve<IStateManager>();
            var contractRegisterer = _container.Resolve<IContractRegisterer>();
            var systemContractReader = _container.Resolve<ISystemContractReader>();
            var privateWallet = _container.Resolve<IPrivateWallet>();
            var tx = new TransactionReceipt();

            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] publicKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            var sender = keyPair.PublicKey.GetAddress();
            
            var context = new InvocationContext(sender, stateManager.LastApprovedSnapshot, tx);
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
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.BecomeStaker(publicKey, stakeAmount.ToUInt256(), frame));
            }

            // Get stake
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetStake, sender);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetStake(sender, frame));
                Assert.AreEqual(Money.Parse("1000"),frame.ReturnValue.ToUInt256().ToMoney());
            }
            
            // Get Vrf Seed
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetVrfSeed);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetVrfSeed(frame));
                Assert.AreEqual(0, frame.ReturnValue.ToUInt256().ToBigInteger());
            }
            
            // Able to validator
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodIsAbleToBeValidator, sender);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsAbleToBeValidator(sender, frame));
                Assert.AreEqual(1, BitConverter.ToInt32(frame.ReturnValue, 0));
            }

            {
                var tokenUnitsInRoll = BigInteger.Pow(10, 21);
                
                var seed = systemContractReader.GetVrfSeed();
                Console.WriteLine(seed.Length);
                
                var rolls = stakeAmount / tokenUnitsInRoll;
                Console.WriteLine(rolls);
                
                var totalRolls = systemContractReader.GetTotalStake().ToBigInteger() / tokenUnitsInRoll;
                Console.WriteLine(totalRolls);
                
                // var (proof, value, j) = Vrf.Evaluate(privateWallet.EcdsaKeyPair.PrivateKey.Buffer.ToByteArray(), seed,
                //     StakingContract.Role, StakingContract.ExpectedValidatorsCount, rolls, totalRolls);
                //
                // var input = ContractEncoder.Encode(StakingInterface.MethodSubmitVrf, publicKey, proof);
                // var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                // Assert.IsNotNull(call);
                //
                // var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                //
                // var res = contract.SubmitVrf(publicKey, proof, frame);
                
                // Console.WriteLine(res);
                // Assert.AreEqual(ExecutionStatus.Ok, contract.SubmitVrf(publicKey, proof, frame));
            }
        }
    }
}