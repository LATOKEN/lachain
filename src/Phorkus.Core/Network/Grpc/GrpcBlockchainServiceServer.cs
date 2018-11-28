using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Phorkus.Core.Storage;
using Phorkus.Proto;
using static Phorkus.Network.Grpc.BlockchainService;

namespace Phorkus.Core.Network.Grpc
{
    public class GrpcBlockchainServiceServer : BlockchainServiceBase
    {
        private readonly INetworkContext _networkContext;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly IBlockSynchronizer _blockSynchronizer;

        public GrpcBlockchainServiceServer(
            INetworkContext networkContext,
            ITransactionRepository transactionRepository,
            IBlockRepository blockRepository,
            IBlockSynchronizer blockSynchronizer)
        {
            _networkContext = networkContext;
            _transactionRepository = transactionRepository;
            _blockRepository = blockRepository;
            _blockSynchronizer = blockSynchronizer;
        }

        public override Task<HandshakeReply> Handshake(HandshakeRequest request, ServerCallContext context)
        {
            var reply = new HandshakeReply
            {
                Node = _networkContext.LocalNode
            };
            _blockSynchronizer.HandlePeerHasBlocks(request.Node.BlockHeight);
            return Task.FromResult(reply);
        }
        
        public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
        {
            var reply = new PingReply
            {
                Timestamp = request.Timestamp
            };
            return Task.FromResult(reply);
        }

        public override Task GetBlocksByHashes(GetBlocksByHashesRequest request, IServerStreamWriter<GetBlocksByHashesReply> responseStream, ServerCallContext context)
        {
            var blocks = _blockRepository.GetBlocksByHashes(request.BlockHashes);
            var chunk = new List<Block>();
            foreach (var block in blocks)
            {
                chunk.Add(block);
                if (chunk.Count <= 1024)
                    continue;
                var reply = new GetBlocksByHashesReply
                {
                    Blocks = { chunk }
                };
                responseStream.WriteAsync(reply).Wait();
                chunk.Clear();
            }

            if (chunk.Count > 0)
            {
                var reply = new GetBlocksByHashesReply
                {
                    Blocks = { chunk }
                };
                responseStream.WriteAsync(reply).Wait();
                chunk.Clear();
            }
            return Task.CompletedTask;
        }

        public override Task GetBlocksByHeightRange(GetBlocksByHeightRangeRequest request, IServerStreamWriter<GetBlocksByHeightRangeReply> responseStream,
            ServerCallContext context)
        {
            var blocks = _blockRepository.GetBlocksByHeightRange(request.FromHeight, request.ToHeight);
            var reply = new GetBlocksByHeightRangeReply
            {
                BlockHashes = {blocks.Select(block => block.Hash)}
            };
            return responseStream.WriteAsync(reply);
        }

        public override Task GetTransactionsByHashes(GetTransactionsByHashesRequest request, IServerStreamWriter<GetTransactionsByHashesReply> responseStream,
            ServerCallContext context)
        {
            var transactions = _transactionRepository.GetTransactionsByHashes(request.TransactionHashes);
            var reply = new GetTransactionsByHashesReply
            {
                Transactions = {transactions}
            };
            return responseStream.WriteAsync(reply);
        }
        
        public override Task GetTransactionHashesByBlockHeight(GetTransactionHashesByBlockHeightRequest request,
            IServerStreamWriter<GetTransactionHashesByBlockHeightReply> responseStream, ServerCallContext context)
        {
            var block = _blockRepository.GetBlockByHeight(request.BlockHeight);
            var reply = new GetTransactionHashesByBlockHeightReply
            {
                TransactionHashes = {block.TransactionHashes}
            };
            return responseStream.WriteAsync(reply);
        }
    }
}