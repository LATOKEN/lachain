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

namespace Lachain.CoreTest.RPC.HTTP.Web3
{
    public class Web3ValidatorServiceTest
    {
        private IContainer? _container;
        private IPrivateWallet _privateWallet = null!;
        private IValidatorStatusManager _validatorStatusManager = null!;

        private ValidatorServiceWeb3 _apiService = null!;

        [SetUp]
        public void Setup()
        {

            _container?.Dispose();
            TestUtils.DeleteTestChainData();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
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

            ServiceBinder.BindService<GenericParameterAttributes>();
            _apiService = new ValidatorServiceWeb3(_validatorStatusManager, _privateWallet);


        }

        [TearDown]
        public void Teardown()
        {
            _validatorStatusManager.Stop();
            var sessionId = Handler.DefaultSessionId();
            Handler.DestroySession(sessionId);
            _container?.Dispose();
            TestUtils.DeleteTestChainData();

        }


        [Test]
        [Repeat(2)]
        // changed from private to public: StartValidator()
        public void Test_StartValidator() 
        {
            Assert.AreEqual("wallet_locked" , _apiService.StartValidator());
            string passWord = "12345";
            Assert.AreEqual(true, _privateWallet.Unlock(passWord,100) );
            Assert.AreEqual("validator_started" , _apiService.StartValidator());
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: StartValidatorWithStake()
        public void Test_StartValidatorWithStake() 
        {
            string money = "2000";
            Assert.AreEqual("wallet_locked", _apiService.StartValidatorWithStake(money));
            string passWord = "12345";
            Assert.AreEqual(true, _privateWallet.Unlock(passWord, 100));
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
            _validatorStatusManager.Start(true);
            Assert.AreEqual("0x002" , _apiService.GetValidatorStatus());
        }

        [Test]
        [Repeat(2)]
        // changed from private to public: StopValidator() , IsMining()
        public void Test_StopValidator() 
        {
            Assert.AreEqual("wallet_locked", _apiService.StopValidator());
            string passWord = "12345";
            Assert.AreEqual(true, _privateWallet.Unlock(passWord, 100));
            string money = "2000";
            _validatorStatusManager.StartWithStake(Money.Parse(money).ToUInt256());
            Assert.AreEqual(true, _apiService.IsMining());
            Assert.AreEqual("validator_stopped", _apiService.StopValidator());
            // this does not work because this is the only validator;
            //Assert.AreEqual(false, _apiService.IsMining());
        }

        

    }
}