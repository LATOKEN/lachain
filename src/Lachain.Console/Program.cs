using System;
using System.IO;
using System.Text;
using CommandLine;
using Lachain.Core.CLI;
using Lachain.Crypto;
using System.Linq;
using Lachain.Storage;
using Lachain.Storage.State;
using Lachain.Core.DI;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Config;
using System.Reflection;
using Lachain.Core.DI.Modules;
using  Lachain.Storage.Repositories;
using Lachain.Storage.DbCompact;
using NLog;

namespace Lachain.Console
{
    class Program
    {
        internal static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<RunOptions, DecryptOptions, EncryptOptions, KeygenOptions, DbOptions>(args)
                .WithParsed<RunOptions>(RunNode)
                .WithParsed<DecryptOptions>(DecryptWallet)
                .WithParsed<EncryptOptions>(EncryptWallet)
                .WithParsed<KeygenOptions>(RunKeygen)
                .WithParsed<DbOptions>(UpdateDb)
                .WithNotParsed(errors =>
                {
                    foreach (var error in errors)
                    {
                        if (error is VersionRequestedError) continue;
                        System.Console.Error.WriteLine(error);
                    }
                });

            // GenWallet(
            //     "wallet.json", 
            //     "d95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48", 
            //     "0x000000000000000000000000000000000000000000000000000000000000000000000000",
            //     "0xcb436d851f7d58773a36daf94350f25635b06fb970dc670059529f6b3797b668"
            // );
        }

        private static void RunKeygen(KeygenOptions options)
        {
            if(options.hardforks.ToList().Count != 3) throw new ArgumentException("number of hardfork heights should be 3");
            TrustedKeygen.DoKeygen(options.N, options.F, options.IpAddresses, options.port, options.target, options.chainid,  options.cycleDuration, options.validatorsCount, options.networkName,
                options.feedAddress, options.feedBalance, options.stakeAmount, options.hardforks);
        }

        private static void EncryptWallet(EncryptOptions options)
        {
            string path = options.WalletPath;
            path = Path.IsPathRooted(path) || path.StartsWith("~/")
                ? path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(path)), path);
            var content = File.ReadAllBytes(path) ?? throw new Exception($"Cannot read file {path}");
            var key = Encoding.UTF8.GetBytes(options.WalletPassword).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            var encryptedContent = crypto.AesGcmEncrypt(key, content);
            using var stream = System.Console.OpenStandardOutput();
            stream.Write(encryptedContent, 0, encryptedContent.Length);
        }

        private static void DecryptWallet(DecryptOptions options)
        {
            string path = options.WalletPath;
            path = Path.IsPathRooted(path) || path.StartsWith("~/")
                ? path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(path)), path);
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(options.WalletPassword).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            var decryptedContent =
                Encoding.UTF8.GetString(crypto.AesGcmDecrypt(key, encryptedContent));
            System.Console.WriteLine(decryptedContent);
        }

        private static void RunNode(RunOptions options)
        {
            using var app = new Application(options.ConfigPath, options);
            app.Start(options);
        }
        
        private static void UpdateDb(DbOptions options)
        {
            var logLevel = Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (logLevel != null) logLevel = char.ToUpper(logLevel[0]) + logLevel.ToLower().Substring(1);
            if (!new[] {"Trace", "Debug", "Info", "Warn", "Error", "Fatal"}.Contains(logLevel))
                logLevel = "Trace";
            LogManager.Configuration.Variables["consoleLogLevel"] = logLevel;
            LogManager.ReconfigExistingLoggers();
            
            if(options.type == "soft")
            {
                IRocksDbContext dbContext = new RocksDbContext();
                dbContext.CompactAll();
            }
            else if(options.type == "hard")
            {
                // consider taking a backup of the folder ChainLachain in case anything goes wrong
                if(options.depth <= 0) throw new ArgumentException("depth must be positive integer");

                var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(
                        "./config.json",
                        new RunOptions()
                    ));
                containerBuilder.RegisterModule<StorageModule>();
                IContainer _container = containerBuilder.Build();
                var stateManager = _container.Resolve<IStateManager>();
                var dbShrink = _container.Resolve<IDbShrink>();
                var dbContext = _container.Resolve<IRocksDbContext>(); 

                dbShrink.ShrinkDb(options.depth, stateManager.LastApprovedSnapshot.Blocks.GetTotalBlockHeight(), options.consistencyCheck);
                dbContext.CompactAll();
            }
            else 
            {
                throw new Exception("compaction should be of either 'soft' or 'hard' type");
            }
        }
    }
}