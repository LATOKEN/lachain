using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Logger;
using Lachain.UtilityTest;
using NUnit.Framework;
using Newtonsoft.Json;
using System;

namespace Lachain.CoreTest.IntegrationTests
{
    [TestFixture]
    public class ConfigBackupTest
    {
        private static readonly ILogger<ConfigBackupTest> Logger = LoggerFactory.GetLoggerForClass<ConfigBackupTest>();
        private IContainer? _container;
        private IConfigManager _configManager = null!;
        
        public ConfigBackupTest()
        {
           var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();
            _container = containerBuilder.Build();
            _configManager = _container.Resolve<IConfigManager>();

        } 

        [SetUp]
        public void Setup()
        {
            _container?.Dispose() ;
            TestUtils.DeleteTestChainData();
            
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();
            _container = containerBuilder.Build();
            _configManager = _container.Resolve<IConfigManager>();

        }

        [TearDown]
        public void Teardown()
        {
            _container?.Dispose();
            TestUtils.DeleteTestChainData();
        }

        [Test]
        public void TestConfigBackup()
        {
            string configPath = _configManager.ConfigPath;
            string configBackupPath = ((ConfigManager)_configManager).ConfigBackupPath;
            
            CheckConfig(configPath, configBackupPath);

            //Check if backup is used if config is deleted
            File.Delete(configPath);
            ReloadConfig();
            CheckConfig(configPath, configBackupPath);

            //Check if backup is created if not present
            File.Delete(configBackupPath);
            ReloadConfig();
            CheckConfig(configPath, configBackupPath);

            //Check if backup is used if config is corrupted
            System.IO.File.WriteAllText(configPath,string.Empty);
            ReloadConfig();
            CheckConfig(configPath, configBackupPath);

            //Check if backup is recreated if corrupted
            System.IO.File.WriteAllText(configBackupPath,string.Empty);
            ReloadConfig();
            CheckConfig(configPath, configBackupPath);

            //Should throw error if neither file present
            File.Delete(configPath);
            File.Delete(configBackupPath);
            Assert.Throws<System.IO.FileNotFoundException>(() => ReloadConfig());
        }

        private IReadOnlyDictionary<string, object> loadConfig(string _filePath) {
            var body = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
        }

        private void ReloadConfig() {
            _configManager = new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            );
        }

        private void CheckConfig(string configPath, string configBackupPath) {
            Assert.IsTrue(File.Exists(configPath));
            Assert.IsTrue(File.Exists(configBackupPath));

            var config = loadConfig(configPath);
            var configBackup = loadConfig(configPath);
            Assert.AreEqual(config, configBackup);
        }
    }
}