using AustinHarris.JsonRpc;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.RPC.HTTP;
using Phorkus.Core.VM;
using Phorkus.Storage.State;

namespace Phorkus.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IVirtualMachine _virtualMachine;

        public RpcManager(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            IConfigManager configManager,
            IStateManager stateManager,
            ITransactionPool transactionPool,
            IVirtualMachine virtualMachine)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _configManager = configManager;
            _stateManager = stateManager;
            _transactionPool = transactionPool;
            _virtualMachine = virtualMachine;
        }

        private HttpService _httpService;
        
        public void Start()
        {            
            // ReSharper disable once UnusedVariable
            var implicitlyDeclaredAndBoundedServices = new JsonRpcService[]
            {
                new BlockchainService(_transactionManager, _blockManager, _blockchainContext, _stateManager),
                new AccountService(_virtualMachine, _stateManager, _transactionManager, _transactionPool)
            };
            
            var rpcConfig = _configManager.GetConfig<RpcConfig>("rpc") ?? RpcConfig.Default;
            
            _httpService = new HttpService();
            _httpService.Start(rpcConfig);
        }
        
        public void Stop()
        {
            _httpService?.Stop();
        }
    }
}