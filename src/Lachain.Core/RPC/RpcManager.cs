using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.BlockchainFilter;
using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.RPC.HTTP.FrontEnd;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;
using Lachain.Storage.Trie;

namespace Lachain.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockManager _blockManager;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;
        private readonly ISnapshotIndexRepository _snapshotIndexer;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IBlockchainEventFilter _blockchainEventFilter;
        private readonly INetworkManager _networkManager;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly ISystemContractReader _systemContractReader;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ILocalTransactionRepository _localTransactionRepository;
        private readonly INodeRetrieval _nodeRetrieval; 


        public RpcManager(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IConfigManager configManager,
            IStateManager stateManager,
            ISnapshotIndexRepository snapshotIndexer,
            ITransactionPool transactionPool,
            IVirtualMachine virtualMachine,
            IContractRegisterer contractRegisterer,
            IValidatorStatusManager validatorStatusManager,
            ISystemContractReader systemContractReader,
            IBlockSynchronizer blockSynchronizer,
            ILocalTransactionRepository localTransactionRepository,
            ITransactionSigner transactionSigner,
            IPrivateWallet privateWallet,
            ITransactionBuilder transactionBuilder,
            IBlockchainEventFilter blockchainEventFilter,
            INetworkManager networkManager,
            INodeRetrieval nodeRetrieval
        )
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _configManager = configManager;
            _stateManager = stateManager;
            _snapshotIndexer = snapshotIndexer;
            _transactionPool = transactionPool;
            _contractRegisterer = contractRegisterer;
            _validatorStatusManager = validatorStatusManager;
            _systemContractReader = systemContractReader;
            _blockSynchronizer = blockSynchronizer;
            _localTransactionRepository = localTransactionRepository;
            _transactionSigner = transactionSigner;
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _blockchainEventFilter = blockchainEventFilter;
            _networkManager = networkManager;
            _nodeRetrieval = nodeRetrieval;
        }

        private HttpService? _httpService;

        public void Start()
        {
            // ReSharper disable once UnusedVariable
            var implicitlyDeclaredAndBoundedServices = new JsonRpcService[]
            {
                new BlockchainService(_transactionManager, _blockManager, _transactionPool, _stateManager,
                    _blockSynchronizer, _systemContractReader),
                new AccountService(_stateManager, _transactionManager, _transactionPool, _privateWallet, 
                    _transactionBuilder, _transactionSigner),
                new BlockchainServiceWeb3(_transactionManager, _blockManager, _transactionPool, _stateManager, _snapshotIndexer, _networkManager, _nodeRetrieval),
                new AccountServiceWeb3(_stateManager, _snapshotIndexer, _contractRegisterer, _systemContractReader),
                new ValidatorServiceWeb3(_validatorStatusManager, _privateWallet),
                new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionBuilder, _transactionSigner, 
                    _transactionPool, _contractRegisterer, _privateWallet),
                new FrontEndService(_stateManager, _transactionPool, _transactionSigner, _systemContractReader,
                    _localTransactionRepository, _validatorStatusManager, _privateWallet),
                new NodeService(_blockSynchronizer, _blockchainEventFilter, _networkManager)
            };

            RpcConfig rpcConfig;
            if (
                
                !(_configManager.CommandLineOptions.RpcAddress is null) &&
                !_configManager.CommandLineOptions.RpcPort.HasValue &&
                !(_configManager.CommandLineOptions.RpcApiKey is null)
            )
                rpcConfig = new RpcConfig
                {
                    Hosts = new[] {_configManager.CommandLineOptions.RpcAddress!},
                    Port = _configManager.CommandLineOptions.RpcPort!.Value,
                    ApiKey = _configManager.CommandLineOptions.RpcApiKey,
                };
            else
                rpcConfig = _configManager.GetConfig<RpcConfig>("rpc") ?? RpcConfig.Default;

            _httpService = new HttpService();
            _httpService.Start(rpcConfig);
        }

        public void Stop()
        {
            _httpService?.Stop();
        }
    }
}