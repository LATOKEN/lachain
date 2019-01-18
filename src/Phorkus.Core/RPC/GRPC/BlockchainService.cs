using System.Threading.Tasks;
using Grpc.Core;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Proto.Grpc;

namespace Phorkus.Core.RPC.GRPC
{
    public class BlockchainService : Proto.Grpc.BlockchainService.BlockchainServiceBase
    {
        private readonly ITransactionManager _transactionManager;
        private readonly IBlockManager _blockManager;

        public BlockchainService(
            ITransactionManager transactionManager,
            IBlockManager blockManager)
        {
            _transactionManager = transactionManager;
            _blockManager = blockManager;
        }

        public override Task<GetBlockByHeightReply> GetBlockByHeight(GetBlockByHeightRequest request, ServerCallContext context)
        {
            var reply = new GetBlockByHeightReply
            {
                Block = _blockManager.GetByHeight(request.BlockHeight)
            };
            return Task.FromResult(reply);
        }

        public override Task<GetBlockByHashReply> GetBlockByHash(GetBlockByHashRequest request, ServerCallContext context)
        {
            var reply = new GetBlockByHashReply
            {
                Block = _blockManager.GetByHash(request.BlockHash)
            };
            return Task.FromResult(reply);
        }

        public override Task<GetTransactionByHashReply> GetTransactionByHash(GetTransactionByHashRequest request, ServerCallContext context)
        {
            var reply = new GetTransactionByHashReply
            {
                Transaction = _transactionManager.GetByHash(request.TransactionHash)
            };
            return Task.FromResult(reply);
        }
    }
}