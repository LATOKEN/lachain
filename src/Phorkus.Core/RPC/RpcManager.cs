using System;
using Grpc.Core;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Proto.Grpc;

namespace Phorkus.Core.RPC
{
    public class RpcManager : IRpcManager
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;
        private readonly IConfigManager _configManager;

        public RpcManager(
            ITransactionManager transactionManager,
            IBlockManager blockManager,
            IConfigManager configManager)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
            _configManager = configManager;
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
                    BlockchainService.BindService(new GRPC.BlockchainService(_transactionManager, _blockManager))
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