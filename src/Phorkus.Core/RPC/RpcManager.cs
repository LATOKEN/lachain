using System;
using AustinHarris.JsonRpc;
using Grpc.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.RPC.HTTP;
using Phorkus.Storage.State;

namespace Phorkus.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IConfigManager _configManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly IStateManager _stateManager;
        private readonly ITransactionPool _transactionPool;

        public RpcManager(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            IConfigManager configManager,
            ITransactionBuilder transactionBuilder,
            IStateManager stateManager,
            ITransactionPool transactionPool)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _blockchainContext = blockchainContext;
            _configManager = configManager;
            _transactionBuilder = transactionBuilder;
            _stateManager = stateManager;
            _transactionPool = transactionPool;
        }

        private HttpService _httpService;
        
        public void Start()
        {            
            // ReSharper disable once UnusedVariable
            var implicitlyDeclaredAndBoundedServices = new JsonRpcService[]
            {
                new BlockchainService(_transactionManager, _blockManager, _blockchainContext),
                new AccountService(_stateManager, _transactionPool)
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