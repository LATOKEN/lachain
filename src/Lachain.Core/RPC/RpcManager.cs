using AustinHarris.JsonRpc;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Config;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.RPC.HTTP.Web3;
using Lachain.Core.VM;
using Lachain.Storage.State;

namespace Lachain.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IConfigManager _configManager;
        private readonly IStateManager _stateManager;
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

        private HttpService? _httpService;
        
        public void Start()
        {            
            // ReSharper disable once UnusedVariable
            var implicitlyDeclaredAndBoundedServices = new JsonRpcService[]
            {
                new BlockchainService(_transactionManager, _blockManager, _blockchainContext, _transactionPool, _stateManager),
                new AccountService(_virtualMachine, _stateManager, _transactionManager, _transactionPool),
                new BlockchainServiceWeb3(_transactionManager, _blockManager, _blockchainContext, _transactionPool, _stateManager),
                new AccountServiceWeb3(_virtualMachine, _stateManager, _transactionManager, _transactionPool),
                new TransactionServiceWeb3(_virtualMachine, _stateManager, _transactionManager, _transactionPool), 
                new NodeService()
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