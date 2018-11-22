using System;
using NeoSharp.Application.DI;
using NeoSharp.Core;
using NeoSharp.Core.DI;
using NeoSharp.DI.SimpleInjector;

namespace NeoSharp.Application
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, exception) =>
            {
                Console.Error.WriteLine(" ~ Unhandled exception ~");
                Console.Error.WriteLine("-------------------------------");
                Console.Error.WriteLine(exception);
                Console.Error.WriteLine("-------------------------------");
                Environment.Exit(1);
            };
            
            var containerBuilder = new SimpleInjectorContainerBuilder();

            containerBuilder.RegisterModule<CoreModule>();
            containerBuilder.RegisterModule<ConfigurationModule>();
            containerBuilder.RegisterModule<LoggingModule>();
            containerBuilder.RegisterModule<SerializationModule>();
            containerBuilder.RegisterModule<PersistenceModule>();
            containerBuilder.RegisterModule<ClientModule>();
            containerBuilder.RegisterModule<WalletModule>();
            containerBuilder.RegisterModule<VMModule>();

            var container = containerBuilder.Build();

            // FixDb(container).Wait();

            var bootstrapper = container.Resolve<IBootstrapper>();

            bootstrapper.Start(args);
        }
    }
}