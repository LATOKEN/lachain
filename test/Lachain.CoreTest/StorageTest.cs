using System.Numerics;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class StorageTest
    {
        private readonly IContainer _container;

        public StorageTest()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));

            containerBuilder.RegisterModule<StorageModule>();

            _container = containerBuilder.Build();
        }

        [OneTimeTearDown]
        public void DisposeContainer()
        {
            _container.Dispose();
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
        }

        [Test]
        public void Test_AddDelete()
        {
            var stateManager = _container.Resolve<IStateManager>();
            var snapshot = stateManager.NewSnapshot();
            snapshot.Storage.SetValue(UInt160Utils.Zero, UInt256Utils.Zero, UInt256Utils.Zero);
            snapshot.Storage.SetValue(UInt160Utils.Zero, new BigInteger(1).ToUInt256(), UInt256Utils.Zero);
            snapshot.Storage.DeleteValue(UInt160Utils.Zero, UInt256Utils.Zero, out _);
            snapshot.Storage.DeleteValue(UInt160Utils.Zero, UInt256Utils.Zero, out _);
        }
    }
}