using System;
using Phorkus.Core.Config;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Logger;

namespace Phorkus.Faker
{
    class Program
    {
        private static void Main(string[] args)
        {
            var command = args.Length > 0 ? args[0] : "block-generator";
            
            var containerBuilder = new SimpleInjectorContainerBuilder(
                new ConfigManager("config.json"));

            containerBuilder.RegisterModule<LoggingModule>();
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<CryptographyModule>();
            containerBuilder.RegisterModule<MessagingModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();

            var container = containerBuilder.Build();
            
            Console.WriteLine("Starting faker with command: " + command);
            
            new BlockGenerator(5000, 10).Start(container);
        }
    }
}