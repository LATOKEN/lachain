using System;
using System.Linq;
using System.Threading;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.CLI;
using Lachain.Core.Config;
using Lachain.Core.Consensus;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.DI;
using Lachain.Core.DI.Modules;
using Lachain.Core.DI.SimpleInjector;
using Lachain.Core.Network;
using Lachain.Core.RPC;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;
using Lachain.Utility.Utils;
using NLog;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;

namespace Lachain.Console
{
    public class Application : IBootstrapper, IDisposable
    {
        private readonly IContainer _container;
        private static readonly ILogger<Application> Logger = LoggerFactory.GetLoggerForClass<Application>();

        public Application(string configPath, RunOptions options)
        {
            var logLevel = options.LogLevel ?? Environment.GetEnvironmentVariable("LOG_LEVEL");
            if (logLevel != null) logLevel = char.ToUpper(logLevel[0]) + logLevel.ToLower().Substring(1);
            if (!new[] {"Trace", "Debug", "Info", "Warn", "Error", "Fatal"}.Contains(logLevel))
                logLevel = "Info";
            LogManager.Configuration.Variables["consoleLogLevel"] = logLevel;
            LogManager.ReconfigExistingLoggers();

            var containerBuilder = new SimpleInjectorContainerBuilder(new ConfigManager(configPath, options));
            containerBuilder.RegisterModule<BlockchainModule>();
            containerBuilder.RegisterModule<ConfigModule>();
            containerBuilder.RegisterModule<ConsensusModule>();
            containerBuilder.RegisterModule<NetworkModule>();
            containerBuilder.RegisterModule<StorageModule>();
            containerBuilder.RegisterModule<RpcModule>();
            containerBuilder.RegisterModule<ConsoleModule>();
            _container = containerBuilder.Build();
        }

        public void Start(RunOptions options)
        {
            var configManager = _container.Resolve<IConfigManager>();
            var blockManager = _container.Resolve<IBlockManager>();
            var consensusManager = _container.Resolve<IConsensusManager>();
            var validatorStatusManager = _container.Resolve<IValidatorStatusManager>();
            var transactionVerifier = _container.Resolve<ITransactionVerifier>();
            var validatorManager = _container.Resolve<IValidatorManager>();
            var blockSynchronizer = _container.Resolve<IBlockSynchronizer>();
            var networkManager = _container.Resolve<INetworkManager>();
            var commandManager = _container.Resolve<IConsoleManager>();
            var rpcManager = _container.Resolve<IRpcManager>();
            var stateManager = _container.Resolve<IStateManager>();
            var wallet = _container.Resolve<IPrivateWallet>();
            var metricsService = _container.Resolve<IMetricsService>();
            var snapshotIndexRepository = _container.Resolve<ISnapshotIndexRepository>();
            var localTransactionRepository = _container.Resolve<ILocalTransactionRepository>();

            if (options.RollBackTo.HasValue)
            {
                Logger.LogWarning($"Performing roll back to block {options.RollBackTo.Value}");
                var snapshot = snapshotIndexRepository.GetSnapshotForBlock(options.RollBackTo.Value);
                stateManager.RollbackTo(snapshot);
                wallet.DeleteKeysAfterBlock(options.RollBackTo.Value);
                Logger.LogWarning($"Rollback to block {options.RollBackTo.Value} complete");
            }

            if (!(options.SetStateTo is null))
            {
                string _rpcURL = options.SetStateTo;
                ulong blockNumber = Convert.ToUInt64((string)_CallJsonRPCAPI("eth_blockNumber", new JArray{}, _rpcURL), 16);
                Logger.LogWarning($"Performing set state to block {blockNumber}");
                var snapshot = stateManager.NewSnapshot();
                
                Logger.LogInformation($"Reading state in json format");
                JObject? receivedInfo = (JObject?)_CallJsonRPCAPI("la_getStateByNumber", new JArray{Web3DataFormatUtils.Web3Number(blockNumber)}, _rpcURL);
                string[] trieNames = new string[]{"Balances", "Contracts", "Storage", "Transactions", "Blocks", "Events", "Validators"};
                ISnapshot[] snapshots = new ISnapshot[]{snapshot.Balances,
                                                        snapshot.Contracts,
                                                        snapshot.Storage,
                                                        snapshot.Transactions,
                                                        snapshot.Blocks,
                                                        snapshot.Events,
                                                        snapshot.Validators}; 

                for(int i = 0; i < trieNames.Length; i++)
                {
                    var stateStringName = trieNames[i];
                    Logger.LogInformation($"Updating {stateStringName} Trie");
                    var stateStringRootName = stateStringName + "Root";
                    JObject currentTrie = (JObject)receivedInfo[stateStringName];
                    string currentTrieRoot = (string)receivedInfo[stateStringRootName];
                    ulong root = Convert.ToUInt64(currentTrieRoot, 16);
                    IDictionary<ulong, IHashTrieNode> trieNodes = Web3DataFormatUtils.TrieFromJson(currentTrie);
                    snapshots[i].SetState(root, trieNodes);
                    Logger.LogInformation($"{stateStringName} update done");
                }

                stateManager.Approve();
                stateManager.Commit();
                snapshotIndexRepository.SaveSnapshotForBlock(blockNumber, snapshot);
                Logger.LogWarning($"Set state to block {blockNumber} complete");
            }

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

            metricsService.Start();
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
            consensusManager.Start(blockManager.GetHeight() + 1);
            validatorStatusManager.Start(false);

            System.Console.CancelKeyPress += (sender, e) =>
            {
                System.Console.WriteLine("Interrupt received. Exiting...");
                _interrupt = true;
                Dispose();
            };

            while (!_interrupt)
                Thread.Sleep(1000);
        }
        private JToken? _CallJsonRPCAPI(string method, JArray param, string _rpcURL)
        {
            JObject options = new JObject{
                ["method"] = method,
                ["jsonrpc"] = "2.0",
                ["id"] = "1"
            };
            if (param.Count != 0) options["params"] = param;
            var webRequest = (HttpWebRequest) WebRequest.Create(_rpcURL);
            webRequest.ContentType = "application/json";
            webRequest.Method = "POST";
            using (Stream dataStream = webRequest.GetRequestStream())
            {
                string payloadString = JsonConvert.SerializeObject(options);
                byte[] byteArray = Encoding.UTF8.GetBytes(payloadString);
                dataStream.Write(byteArray, 0, byteArray.Length);
            }

            WebResponse webResponse;
            JObject response;
            using (webResponse = webRequest.GetResponse())
            {
                using (Stream str = webResponse.GetResponseStream()!)
                {
                    using (StreamReader sr = new StreamReader(str))
                    {
                        response = JsonConvert.DeserializeObject<JObject>(sr.ReadToEnd());
                    }
                }
            }
            var result = response["result"];
            return result;
        }
        private bool _interrupt;

        public void Dispose()
        {
            _container.Dispose();
        }
    }
}