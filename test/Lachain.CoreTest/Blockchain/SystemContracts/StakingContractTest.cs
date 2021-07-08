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
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
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
            var tx = new TransactionReceipt();
            var sender = new BigInteger(0).ToUInt160();
            var context = new InvocationContext(sender, stateManager.LastApprovedSnapshot, tx);
            var contract = new StakingContract(context);
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48"
                .HexToBytes().ToPrivateKey());
            byte[] pubKey = CryptoUtils.EncodeCompressed(keyPair.PublicKey);
            ECDSAPublicKey[] allKeys = {keyPair.PublicKey};
            var keygen = new TrustlessKeygen(keyPair, allKeys, 0, 0);
            var cycle = 0.ToUInt256();
            ValueMessage value;
            
        }
    }
}