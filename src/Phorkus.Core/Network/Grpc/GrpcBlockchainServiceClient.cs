using System;
using System.Collections.Generic;
using Grpc.Core;
using Phorkus.Proto;
using static Phorkus.Network.Grpc.BlockchainService;

namespace Phorkus.Core.Network.Grpc
{
    public class GrpcBlockchainServiceClient : IBlockchainService
    {
        private readonly BlockchainServiceClient _client;

        public GrpcBlockchainServiceClient(PeerAddress peerAddress)
        {
            _client = new BlockchainServiceClient(
                new Channel(peerAddress.Host, peerAddress.Port, ChannelCredentials.Insecure));
        }

        public HandshakeReply Handshake(HandshakeRequest request)
        {
            try
            {
                return _client.Handshake(request);
            }
            catch (Exception)
            {
                // ignore
            }

            return null;
        }

        public PingReply Ping(PingRequest request)
        {
            return _client.Ping(request);
        }

        public IEnumerable<Block> GetBlocksByHashes(IEnumerable<UInt256> blockHashes)
        {
            var request = new GetBlocksByHashesRequest
            {
                BlockHashes = {blockHashes}
            };
            var reply = _client.GetBlocksByHashes(request);
            return _streamToEnumerator(reply.ResponseStream, body => body.Blocks.GetEnumerator());
        }

        public IEnumerable<UInt256> GetBlocksHashesByHeightRange(ulong fromBlock, ulong toBlock)
        {
            var request = new GetBlocksByHeightRangeRequest
            {
                FromHeight = fromBlock,
                ToHeight = toBlock
            };
            var reply = _client.GetBlocksByHeightRange(request);
            return _streamToEnumerator(reply.ResponseStream, body => body.BlockHashes.GetEnumerator());
        }

        public IEnumerable<SignedTransaction> GetTransactionsByHashes(IEnumerable<UInt256> transactionHashes)
        {
            var request = new GetTransactionsByHashesRequest
            {
                TransactionHashes = { transactionHashes }
            };
            var reply = _client.GetTransactionsByHashes(request);
            return _streamToEnumerator(reply.ResponseStream, body => body.Transactions.GetEnumerator());
        }

        public IEnumerable<UInt256> GetTransactionHashesByBlockHeight(ulong blockHeight)
        {
            var request = new GetTransactionHashesByBlockHeightRequest
            {
                BlockHeight = blockHeight
            };
            var reply = _client.GetTransactionHashesByBlockHeight(request);
            return _streamToEnumerator(reply.ResponseStream, body => body.TransactionHashes.GetEnumerator());
        }

        public IEnumerable<UInt256> GetMemoryPool()
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Node> GetNeighbours()
        {
            throw new NotImplementedException();
        }
        
        private IEnumerable<TK> _streamToEnumerator<TE, TK>(IAsyncEnumerator<TE> asyncEnumerator, Func<TE, IEnumerator<TK>> mapper)
            where TK : class
            where TE : class
        {
            while (asyncEnumerator.MoveNext().Result)
            {
                var current = asyncEnumerator.Current;
                if (current is null)
                    continue;
                var enumerator = mapper.Invoke(current);
                while (enumerator.MoveNext())
                    yield return enumerator.Current;
            }
        }
    }
}