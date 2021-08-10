using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.RPC.HTTP.FrontEnd;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.UtilityTest;
using NUnit.Framework;

namespace Lachain.CoreTest.RPC.HTTP.FrontEnd
{
    public class FrontEndServiceTest
    {
        private IContainer? _container;
        private IStateManager? _stateManager;
        private ITransactionPool? _transactionPool;
        private ITransactionSigner? _transactionSigner;
        private ISystemContractReader? _systemContractReader;
        private ILocalTransactionRepository? _localTransactionRepository;
        private IValidatorStatusManager? _validatorStatusManager;
        private IPrivateWallet? _privateWallet;

        private FrontEndService? _fes;

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
            _transactionPool = _container.Resolve<ITransactionPool>();
            _transactionSigner = _container.Resolve<ITransactionSigner>();
            _systemContractReader = _container.Resolve<ISystemContractReader>();
            _localTransactionRepository = _container.Resolve<ILocalTransactionRepository>();
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _validatorStatusManager = _validatorStatusManager = new ValidatorStatusManager(
                _transactionPool, _container.Resolve<ITransactionSigner>(), _container.Resolve<ITransactionBuilder>(),
                _privateWallet, _stateManager, _container.Resolve<IValidatorAttendanceRepository>(),
                _container.Resolve<ISystemContractReader>()
            );
            
            _fes = new FrontEndService(_stateManager, _transactionPool, _transactionSigner,
                _systemContractReader, _localTransactionRepository, _validatorStatusManager, _privateWallet);
        }

        [TearDown]
        public void Teardown()
        {
            TestUtils.DeleteTestChainData();
            _container?.Dispose();
        }

        /*
        [Test]
        public void Test_PasswordChange()
        {
            // Change wallet password
            Assert.AreEqual("password_changed", _fes?.ChangePassword("12345", "abcde"));
            
            // Unlock the wallet with incorrect password
            Assert.AreEqual("incorrect_password", _fes?.UnlockWallet("12345", 5));
            
            // Unlock the wallet for 5 seconds with correct password 
            Assert.AreEqual("unlocked", _fes?.UnlockWallet("abcde", 5));
            
            // Check the wallet lock status
            Assert.AreEqual("0x0", _fes?.IsWalletLocked());
            
            // Check the wallet lock status after 10 seconds
            Thread.Sleep(10000);
            Assert.AreEqual("0x1", _fes?.IsWalletLocked());
        }
    */
    }
}