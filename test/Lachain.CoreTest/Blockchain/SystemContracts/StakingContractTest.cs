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
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using LibVRF.Net;
using NUnit.Framework;

namespace Lachain.CoreTest.Blockchain.SystemContracts
{
    public class StakingContractTest
    {
        private readonly IContainer _container;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

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
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] pubKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            ECDSAPublicKey[] allKeys = {keyPair.PublicKey};

            {
                var stake = systemContractReader.GetStake().ToBigInteger();
                
                var seed = systemContractReader.GetVrfSeed();
                var rolls = stake / StakingContract.TokenUnitsInRoll;
                var totalRolls = systemContractReader.GetTotalStake().ToBigInteger() / StakingContract.TokenUnitsInRoll;
                var (proof, value, j) = Vrf.Evaluate(privateWallet.EcdsaKeyPair.PrivateKey.Buffer.ToByteArray(), seed,
                    StakingContract.Role, StakingContract.ExpectedValidatorsCount, rolls, totalRolls);
                
                
                var input = ContractEncoder.Encode(StakingInterface.MethodSubmitVrf, pubKey, proof);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.SubmitVrf(pubKey, proof, frame));
            }
            
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodGetVrfSeed);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.GetVrfSeed(frame));
            }
            
            {
                var staker = systemContractReader.NodePublicKey().ToUInt160();
                var input = ContractEncoder.Encode(StakingInterface.MethodIsAbleToBeValidator, staker);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsAbleToBeValidator(staker, frame));
            }
            
            {
                var input = ContractEncoder.Encode(StakingInterface.MethodIsNextValidator, pubKey);
                var call = contractRegisterer.DecodeContract(context, ContractRegisterer.StakingContract, input);
                Assert.IsNotNull(call);
                var frame = new SystemContractExecutionFrame(call!, context, input, 100_000_000);
                Assert.AreEqual(ExecutionStatus.Ok, contract.IsNextValidator(pubKey, frame));
            }
        }
    }
}