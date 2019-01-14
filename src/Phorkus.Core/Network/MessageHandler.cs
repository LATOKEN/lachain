using System;
using System.Linq;
using Phorkus.Core.Consensus;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;

namespace Phorkus.Core.Network
{
    public class MessageHandler : IMessageHandler
    {
        private readonly IBlockSynchronizer _blockSynchronizer;
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBlockRepository _blockRepository;
        private readonly IGlobalRepository _globalRepository;
        private readonly IConsensusManager _consensusManager;
        private readonly ILogger<MessageHandler> _logger;


        public MessageHandler(
            IBlockSynchronizer blockSynchronizer,
            ITransactionRepository transactionRepository,
            IBlockRepository blockRepository,
            IGlobalRepository globalRepository,
            IConsensusManager consensusManager, 
            ILogger<MessageHandler> logger)
        {
            _blockSynchronizer = blockSynchronizer;
            _transactionRepository = transactionRepository;
            _blockRepository = blockRepository;
            _globalRepository = globalRepository;
            _consensusManager = consensusManager;
            _logger = logger;
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
            var blockHashes = _blockRepository
                .GetBlocksByHeightRange(request.FromHeight, request.ToHeight - request.FromHeight + 1)
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

        public void ConsensusMessage(MessageEnvelope buildEnvelope, ConsensusMessage message)
        {
            switch (message.PayloadCase)
            {
                case Proto.ConsensusMessage.PayloadOneofCase.BlockPrepareRequest:
                    _consensusManager.OnPrepareRequestReceived(message.BlockPrepareRequest);
                    break;
                case Proto.ConsensusMessage.PayloadOneofCase.BlockPrepareReply:
                    _consensusManager.OnPrepareResponseReceived(message.BlockPrepareReply);
                    break;
                case Proto.ConsensusMessage.PayloadOneofCase.ChangeViewRequest:
                    _consensusManager.OnChangeViewReceived(message.ChangeViewRequest);
                    break;
                default:
                    _logger.LogWarning("Ignored unknown consensus payload of type " + message.PayloadCase);
                    break;
            }
        }
    }
}