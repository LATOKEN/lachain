using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using App.Metrics;
using App.Metrics.AspNetCore;
using App.Metrics.Formatters.Prometheus;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.DI;
using Lachain.Core.Network;
using Lachain.Core.RPC;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Utility.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace Lachain.Console
{
    public class Application : IDisposable
    {
        private readonly IWebHost _host;
        private static readonly ILogger<Application> Logger = LoggerFactory.GetLoggerForClass<Application>();

        public Application(string configPath, string[] args, RunOptions options)
        {
            var logLevel = options.LogLevel ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (logLevel != null) logLevel = char.ToUpper(logLevel[0]) + logLevel.ToLower().Substring(1);
            if (!new[] {"Trace", "Debug", "Info", "Warn", "Error", "Fatal"}.Contains(logLevel))
                logLevel = "Info";
            LogManager.Configuration.Variables["consoleLogLevel"] = logLevel;
            LogManager.ReconfigExistingLoggers();

            var configManager = new ConfigManager(configPath, options);
            _host = CreateHostBuilder(args, configManager).Build();
        }

        private static IWebHostBuilder CreateHostBuilder(string[] args, IConfigManager configManager)
        {
            var metrics = new MetricsBuilder()
                .OutputMetrics.AsPrometheusProtobuf()
                .Build();
            return new WebHostBuilder()
                .UseKestrel()
                .UseUrls("http://*:7070") 
                .UseStartup<Startup>()
                .ConfigureMetrics()
                .UseMetrics(
                    options =>
                    {
                        options.EndpointOptions = endpointsOptions =>
                        {
                            endpointsOptions.MetricsEndpointOutputFormatter = metrics.OutputMetricsFormatters
                                .OfType<MetricsPrometheusProtobufOutputFormatter>().First();
                        };
                    }
                )
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<IMetricsRoot>(_ => metrics);
                    services.AddSingleton(_ => configManager);
                    BlockchainModule.AddServices(services);
                    ConsensusModule.AddServices(services);
                    NetworkModule.AddServices(services);
                    StorageModule.AddServices(services, configManager);
                    RpcModule.AddServices(services);
                    ConsoleModule.AddServices(services);
                });
        }

        public Task Start(RunOptions options)
        {
            var configManager = _host.Services.GetService<IConfigManager>()!;
            var blockManager = _host.Services.GetService<IBlockManager>()!;
            var consensusManager = _host.Services.GetService<IConsensusManager>()!;
            var validatorStatusManager = _host.Services.GetService<IValidatorStatusManager>()!;
            var transactionVerifier = _host.Services.GetService<ITransactionVerifier>()!;
            var validatorManager = _host.Services.GetService<IValidatorManager>()!;
            var blockSynchronizer = _host.Services.GetService<IBlockSynchronizer>()!;
            var networkManager = _host.Services.GetService<INetworkManager>()!;
            var commandManager = _host.Services.GetService<IConsoleManager>()!;
            var rpcManager = _host.Services.GetService<IRpcManager>()!;
            var stateManager = _host.Services.GetService<IStateManager>()!;
            var wallet = _host.Services.GetService<IPrivateWallet>()!;
            var localTransactionRepository = _host.Services.GetService<ILocalTransactionRepository>()!;
            var contractRegisterer = _host.Services.GetService<IContractRegisterer>()!;
            ContractInvoker.Init(contractRegisterer);

            localTransactionRepository.SetWatchAddress(wallet.EcdsaKeyPair.PublicKey.GetAddress());

            if (blockManager.TryBuildGenesisBlock())
                Logger.LogInformation("Generated genesis block");

            var genesisBlock = stateManager.LastApprovedSnapshot.Blocks.GetBlockByHeight(0)
                               ?? throw new Exception("Genesis block was not persisted");
            Logger.LogInformation("Genesis Block: " + genesisBlock.Hash.ToHex());
            Logger.LogInformation($" + prevBlockHash: {genesisBlock.Header.PrevBlockHash.ToHex()}");
            Logger.LogInformation($" + merkleRoot: {genesisBlock.Header.MerkleRoot.ToHex()}");
            Logger.LogInformation($" + nonce: {genesisBlock.Header.Nonce}");
            Logger.LogInformation($" + transactionHashes: {genesisBlock.TransactionHashes.ToArray().Length}");
            foreach (var s in genesisBlock.TransactionHashes)
                Logger.LogInformation($" + - {s.ToHex()}");
            Logger.LogInformation($" + hash: {genesisBlock.Hash.ToHex()}");

            Logger.LogInformation("Current block height: " + blockManager.GetHeight());
            Logger.LogInformation($"Node public key: {wallet.EcdsaKeyPair.PublicKey.EncodeCompressed().ToHex()}");
            Logger.LogInformation($"Node address: {wallet.EcdsaKeyPair.PublicKey.GetAddress().ToHex()}");

            var networkConfig = configManager.GetConfig<NetworkConfig>("network") ??
                                throw new Exception("No 'network' section in config file");

            networkManager.Start();
            transactionVerifier.Start();
            commandManager.Start(wallet.EcdsaKeyPair);
            rpcManager.Start();

            blockSynchronizer.Start();
            Logger.LogInformation("Synchronizing blocks...");
            blockSynchronizer.SynchronizeWith(
                validatorManager.GetValidatorsPublicKeys((long) blockManager.GetHeight())
                    .Where(key => !key.Equals(wallet.EcdsaKeyPair.PublicKey))
            );
            Logger.LogInformation("Block synchronization finished, starting consensus...");
            consensusManager.Start((long) blockManager.GetHeight() + 1);
            validatorStatusManager.Start(false);

            // System.Console.CancelKeyPress += (sender, e) =>
            // {
            //     System.Console.WriteLine("Interrupt received. Exiting...");
            //     _interrupt = true;
            //     Dispose();
            // };

            var task = _host.RunAsync();

            while (!_interrupt)
                Thread.Sleep(1000);

            return task;
        }

        private bool _interrupt;

        public void Dispose()
        {
            _host.Dispose();
        }
    }
}