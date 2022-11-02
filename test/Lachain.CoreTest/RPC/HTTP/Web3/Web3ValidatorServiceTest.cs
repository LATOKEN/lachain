using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.Vault;
using Lachain.UtilityTest;
using NUnit.Framework;
using AustinHarris.JsonRpc;
using Lachain.Core.ValidatorStatus;
using Lachain.Utility;
using System.Threading;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.Interface;

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3ValidatorServiceTest
    {
        private IContainer? _container;
        private IPrivateWallet _privateWallet = null!;
        private IValidatorStatusManager _validatorStatusManager = null!;
        private ITransactionBuilder _transactionBuilder = null!;

        private IConfigManager _configManager = null!;

        private ValidatorServiceWeb3 _apiService = null!;

        [SetUp]
        public void Setup()
        {

            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config2.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();

            _container = containerBuilder.Build();

           
            _privateWallet = _container.Resolve<IPrivateWallet>();
            _validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            _configManager = _container.Resolve<IConfigManager>();
            _transactionBuilder = _container.Resolve<ITransactionBuilder>();

            ServiceBinder.BindService<GenericParameterAttributes>();
            _apiService = new ValidatorServiceWeb3(_validatorStatusManager, _privateWallet, _transactionBuilder);


        }

        [TearDown]
        public void Teardown()
        {
            _validatorStatusManager.Stop();
            
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var sessionId = Handler.GetSessionHandler().SessionId;
            if(sessionId != null) Handler.DestroySession(sessionId);

        }


        [Test]
        [Repeat(2)]
        // changed from private to public: StartValidator()
        public void Test_StartValidator() 
        {
            Assert.AreEqual("wallet_locked" , _apiService.StartValidator());
            UnlockWallet(false);
            Assert.AreEqual("wallet_locked" , _apiService.StartValidator());
            UnlockWallet(true);
            Assert.AreEqual("validator_started" , _apiService.StartValidator());
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: StartValidatorWithStake()
        public void Test_StartValidatorWithStake() 
        {
            string money = "2000";
            Assert.AreEqual("wallet_locked", _apiService.StartValidatorWithStake(money));
            UnlockWallet(true);
            Assert.AreEqual("validator_started", _apiService.StartValidatorWithStake(money));

        }

        [Test]
        [Repeat(2)]
        // changed from private to public: GetValidatorStatus()
        public void Test_GetValidatorStatus() 
        {
            Assert.AreEqual("0x00" , _apiService.GetValidatorStatus());
            _validatorStatusManager.Start(false);
            Assert.AreEqual("0x01" , _apiService.GetValidatorStatus());
            _validatorStatusManager.Stop();
            Thread.Sleep(2000);
            Assert.AreEqual("0x00" , _apiService.GetValidatorStatus());
            _validatorStatusManager.Start(true);
            Assert.AreEqual("0x002" , _apiService.GetValidatorStatus());
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: StopValidator() , IsMining()
        public void Test_StopValidator() 
        {
            Assert.AreEqual("wallet_locked", _apiService.StopValidator());
            UnlockWallet(true);
            string money = "2000";
            _validatorStatusManager.StartWithStake(Money.Parse(money).ToUInt256());
            Assert.AreEqual(true, _apiService.IsMining());
            Assert.AreEqual("validator_stopped", _apiService.StopValidator());
            // this does not work because this is the only validator;
            //Assert.AreEqual(false, _apiService.IsMining());
        }

        public void UnlockWallet(bool unlock, int time = 100){
            string password;
            if(unlock){
                var config = _configManager.GetConfig<VaultConfig>("vault") ??
                         throw new Exception("No 'vault' section in config file");
                password = config.ReadWalletPassword();
                Assert.AreEqual(true,_privateWallet.Unlock(password, time));
            }
            else{
                password = "12345" ; // random password
                Assert.AreEqual(false,_privateWallet.Unlock(password, time));
            }
        }

    }
}