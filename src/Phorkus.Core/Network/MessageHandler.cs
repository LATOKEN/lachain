using System;
using System.Linq;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.Networking;
using Phorkus.Proto;

namespace Phorkus.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly IGlobalRepository _globalRepository;

        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            ITransactionRepository transactionRepository,
            IBlockRepository blockRepository,
            IGlobalRepository globalRepository)
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionRepository = transactionRepository;
            _blockRepository = blockRepository;
            _globalRepository = globalRepository;
        }

        public void PingRequest(MessageEnvelope envelope, PingRequest request)
        {
            var reply = envelope.MessageFactory.PingReply(request.Timestamp, _globalRepository.GetTotalBlockHeight());
            envelope.RemotePeer.Send(reply);
        }

        public void PingReply(MessageEnvelope envelope, PingReply reply)
        {
            _blockSynchronizer.HandlePeerHasBlocks(reply.BlockHeight, envelope.RemotePeer);
        }

        public void GetBlocksByHashesRequest(MessageEnvelope envelope, GetBlocksByHashesRequest request)
        {
            var blocks = _blockRepository.GetBlocksByHashes(request.BlockHashes);
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHashesReply(blocks));
        }

        public void GetBlocksByHashesReply(MessageEnvelope envelope, GetBlocksByHashesReply reply)
        {
            var orderedBlocks = reply.Blocks.OrderBy(block => block.Header.Index).ToArray();
            foreach (var block in orderedBlocks)
                _blockSynchronizer.HandleBlockFromPeer(block, envelope.RemotePeer, TimeSpan.FromSeconds(5));
        }
        
        public void GetBlocksByHeightRangeRequest(MessageEnvelope envelope, GetBlocksByHeightRangeRequest request)
        {
            var blockHashes = _blockRepository.GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
                .Select(block => block.Hash);
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHeightRangeReply(blockHashes));
        }

        public void GetBlocksByHeightRangeReply(MessageEnvelope envelope, GetBlocksByHeightRangeReply reply)
        {
            envelope.RemotePeer.Send(envelope.MessageFactory.GetBlocksByHashesRequest(reply.BlockHashes));
        }

        public void GetTransactionsByHashesRequest(MessageEnvelope envelope, GetTransactionsByHashesRequest request)
        {
            var txs = _transactionRepository.GetTransactionsByHashes(request.TransactionHashes);
            envelope.RemotePeer.Send(envelope.MessageFactory.GetTransactionsByHashesReply(txs));
        }
    
        public void GetTransactionsByHashesReply(MessageEnvelope envelope, GetTransactionsByHashesReply reply)
        {
            _blockSynchronizer.HandleTransactionsFromPeer(reply.Transactions, envelope.RemotePeer);
        }
    }
}