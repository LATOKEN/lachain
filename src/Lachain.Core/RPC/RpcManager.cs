using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.RPC.HTTP.FrontEnd;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.ValidatorStatus;
using Lachain.Core.Vault;
using Lachain.Storage.Repositories;
using Lachain.Storage.State;

namespace Lachain.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockManager _blockManager;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;
        private readonly IVirtualMachine _virtualMachine;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionSigner _transactionSigner;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IContractRegisterer _contractRegisterer;
        private readonly IValidatorStatusManager _validatorStatusManager;
        private readonly ISystemContractReader _systemContractReader;
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ILocalTransactionRepository _localTransactionRepository;
        

        public RpcManager(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IConfigManager configManager,
            IStateManager stateManager,
            ITransactionPool transactionPool,
            IVirtualMachine virtualMachine,
            IContractRegisterer contractRegisterer,
            IValidatorStatusManager validatorStatusManager,
            ISystemContractReader systemContractReader, 
            IBlockSynchronizer blockSynchronizer, 
            ILocalTransactionRepository localTransactionRepository, 
            ITransactionSigner transactionSigner, IPrivateWallet privateWallet, ITransactionBuilder transactionBuilder)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _configManager = configManager;
            _stateManager = stateManager;
            _transactionPool = transactionPool;
            _virtualMachine = virtualMachine;
            _contractRegisterer = contractRegisterer;
            _validatorStatusManager = validatorStatusManager;
            _systemContractReader = systemContractReader;
            _blockSynchronizer = blockSynchronizer;
            _localTransactionRepository = localTransactionRepository;
            _transactionSigner = transactionSigner;
            _privateWallet = privateWallet;
            _transactionBuilder = transactionBuilder;
        }

        private HttpService? _httpService;

        public void Start()
        {
            // ReSharper disable once UnusedVariable
            var implicitlyDeclaredAndBoundedServices = new JsonRpcService[]
            {
                new BlockchainService(_transactionManager, _blockManager, _transactionPool, _stateManager, _blockSynchronizer, _systemContractReader),
                new AccountService(_stateManager, _transactionManager, _transactionPool),
                new BlockchainServiceWeb3(_transactionManager, _blockManager, _transactionPool, _stateManager),
                new AccountServiceWeb3(_stateManager),
                new ValidatorServiceWeb3(_validatorStatusManager, _privateWallet), 
                new TransactionServiceWeb3(_stateManager, _transactionManager, _transactionPool, _contractRegisterer),
                new FrontEndService(_stateManager, _transactionPool, _transactionSigner, _systemContractReader, _localTransactionRepository, _validatorStatusManager, _privateWallet, _transactionBuilder), 
                new NodeService(_blockSynchronizer)
            };

            RpcConfig rpcConfig;
            if (
                !(_configManager.GetCliArg("rpcaddr") is null) &&
                !(_configManager.GetCliArg("rpcport") is null) &&
                !(_configManager.GetCliArg("apikey") is null)
            )
                rpcConfig = new RpcConfig
                {
                    Hosts = new[] {_configManager.GetCliArg("rpcaddr")!},
                    Port = short.Parse(_configManager.GetCliArg("rpcport")),
                    ApiKey = _configManager.GetCliArg("apikey"),
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