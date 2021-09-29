using System.Reflection;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Core.DI;
using Lachain.Storage.State;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Vault;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.CLI;
using System.IO;
using Lachain.Core.Config;
using AustinHarris.JsonRpc;
using Lachain.Storage.Repositories;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class AccountServiceWeb3Test
    {

        private IContainer? _container;
        private IStateManager? _stateManager;
        private ISnapshotIndexRepository? _snapshotIndexer;
        private IContractRegisterer? _contractRegisterer;
        private IPrivateWallet? _privateWallet;
        private ISystemContractReader? _systemContractReader;

        private AccountServiceWeb3? _apiService;


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
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _snapshotIndexer = _container.Resolve<ISnapshotIndexRepository>();
            _systemContractReader = _container.Resolve<ISystemContractReader>();

            ServiceBinder.BindService<GenericParameterAttributes>();

            _apiService = new AccountServiceWeb3(_stateManager, _snapshotIndexer, _contractRegisterer, _systemContractReader);
               
        }

        [TearDown]
        public void Teardown()
        {
            var sessionId = Handler.DefaultSessionId();
            Handler.DestroySession(sessionId);

            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        // Changed GetBalance to public
        public void Test_GetBalance()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var bal = _apiService.GetBalance(address, "latest");
            Assert.AreEqual(bal, "0x0");
        }

        [Test] 
        // Changed GetAccounts to public
        public void Test_GetAccounts()
        {
            var account_list = _apiService!.GetAccounts();
            var address = account_list.First.ToString();
            Assert.AreEqual(address, "0x6bc32575acb8754886dc283c2c8ac54b1bd93195");
        }

        [Test]
        // Changed GetTransactionCount to public
        public void Test_GetTransactionCount()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var txCount = _apiService!.GetTransactionCount(address, "latest");
            Assert.AreEqual(txCount, 0);
        }

        [Test]
        // Changed GetCode to public
        public void Test_GetCode()
        {
            var address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195";
            var adCode = _apiService!.GetCode(address, "latest");
            Assert.AreEqual(adCode, "");
        }

    }
}