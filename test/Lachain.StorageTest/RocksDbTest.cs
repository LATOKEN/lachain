using System;
using System.IO;
using System.Reflection;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Logger;
using Lachain.Storage;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using NUnit.Framework;
using Lachain.Proto;
using System.Collections.Generic;
using System.Linq;


namespace Lachain.StorageTest
{
    public class RocksDbTest
    {
        private static readonly ILogger<RocksDbTest> Logger = LoggerFactory.GetLoggerForClass<RocksDbTest>();
        private IContainer _container;
        private IRocksDbContext _dbContext;

        public RocksDbTest()
        {
        }

        [SetUp]
        public void Setup()
        {
            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config.json"),
                new RunOptions()
            ));

            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<StorageModule>();
            _container = containerBuilder.Build();
            _dbContext = _container.Resolve<IRocksDbContext>();
        }

        [TearDown]
        public void TearDown()
        {
            // _dbContext.Dispose();
            _container.Dispose();
            // _dbContext.Dispose();
            TestUtils.DeleteTestChainData();
        }

        // [Test]
        // public void Test()
        // {
        //     var bytes = new byte[1];
        //     _dbContext.Save(bytes, new byte[1] { 1 });

        // }

        [Test]
        [Repeat(1)]
        public void Test_SeekAndSerialized()
        {
            Logger.LogInformation("Started Test_SeekAndSerialized");
            var bytes = new byte[2];
            var values = new List<byte[]>();
            for (int i = 0 ; i < 256; i++)
            {
                for (int j = 0 ; j < 256; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    values.Add(new byte[1]{10});
                    _dbContext.Save(bytes, values.Last());
                }
            }
            
            var startTime = TimeUtils.CurrentTimeMillis();
            var iterator = _dbContext.GetIterator(new byte[1]{0});
            int iter = 0;
            for (int i = 0 ; i < 256; i++)
            {
                for (int j = 0 ; j < 256; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    var key = iterator.Key();
                    var value = iterator.Value();
                    _dbContext.Delete(key);
                    iterator = iterator.Next();
                    Assert.AreEqual(bytes, key);
                    Assert.AreEqual(values[iter], value);
                    // _dbContext.Delete(key);
                    iter++;
                }
            }
            Logger.LogInformation($"Time taken Test_SeekAndSerialized {TimeUtils.CurrentTimeMillis() - startTime}");
        }

        [Test]
        [Repeat(1)]
        public void Test_SeekAndSerialized1()
        {
            Logger.LogInformation("Started Test_SeekAndSerialized1");
            var bytes = new byte[2];
            var values = new List<byte[]>();
            for (int i = 0 ; i < 256; i++)
            {
                for (int j = 0 ; j < 256; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    values.Add(new byte[1]{10});
                    _dbContext.Save(bytes, values.Last());
                }
            }
            
            var startTime = TimeUtils.CurrentTimeMillis();
            var iterator = _dbContext.GetIterator(new byte[1]{0});
            int iter = 0;
            for (int i = 0 ; i < 256; i++)
            {
                for (int j = 0 ; j < 256; j++)
                {
                    bytes[0] = (byte) i;
                    bytes[1] = (byte) j;
                    var key = iterator.Key();
                    var value = iterator.Value();
                    // _dbContext.Delete(key);
                    iterator = iterator.Next();
                    Assert.AreEqual(bytes, key);
                    Assert.AreEqual(values[iter], value);
                    _dbContext.Delete(key);
                    iter++;
                }
            }
            Logger.LogInformation($"Time taken Test_SeekAndSerialized1 {TimeUtils.CurrentTimeMillis() - startTime}");
        }
    }
}