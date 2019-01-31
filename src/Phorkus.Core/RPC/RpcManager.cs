using System;
using Grpc.Core;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Proto.Grpc;
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

        private Server _server;

        public void Start()
        {
            if (_server != null)
                throw new Exception("You already have started GRPC services");
            _server = new Server
            {
                Services =
                {
                    BlockchainService.BindService(new GRPC.BlockchainService(_transactionManager, _blockManager, _blockchainContext)),
                    AccountService.BindService(new GRPC.AccountService(_transactionBuilder, _stateManager, _transactionPool))
                },
                Ports =
                {
                    new ServerPort("0.0.0.0", 6060, ServerCredentials.Insecure)
                }
            };
            _server.Start();
        }
        
        public void Stop()
        {
            _server?.ShutdownAsync().Wait();
        }
    }
}