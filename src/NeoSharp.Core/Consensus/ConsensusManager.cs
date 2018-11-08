using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using NeoSharp.Core.Blockchain;
using NeoSharp.Core.Blockchain.Processing;
using NeoSharp.Core.Consensus.Messages;
using NeoSharp.Core.Extensions;
using NeoSharp.Core.Messaging.Messages;
using NeoSharp.Core.Models;
using NeoSharp.Core.Network;
using NeoSharp.Types;

namespace NeoSharp.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private readonly IBlockProcessor _blockProcessor;
        private readonly IBlockchainContext _blockchainContext;
        private readonly ITransactionCrawler _transactionCrawler;
        private readonly ITransactionProcessor _transactionProcessor;
        private readonly ITransactionPool _transactionPool;
        private readonly ILogger<ConsensusManager> _logger;
        private readonly ConsensusContext _context;

        public ConsensusManager(
            IBlockProcessor blockProcessor, IBlockchainContext blockchainContext,
            ITransactionCrawler transactionCrawler, ITransactionProcessor transactionProcessor,
            ITransactionPool transactionPool, ILogger<ConsensusManager> logger
        )
        {
            _blockProcessor = blockProcessor ?? throw new ArgumentNullException(nameof(blockProcessor));
            _blockchainContext = blockchainContext ?? throw new ArgumentNullException(nameof(blockProcessor));
            _transactionCrawler = transactionCrawler ?? throw new ArgumentNullException(nameof(transactionCrawler));
            _transactionProcessor =
                transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
            _transactionPool = transactionPool ?? throw new ArgumentNullException(nameof(transactionPool));
            _logger = logger ?? throw new ArgumentNullException(nameof(blockProcessor));
            _context = new ConsensusContext();
        }

        public void HandleConsensusMessage(ConsensusMessage message)
        {
            ConsensusPayloadUnsigned body = message.Payload.Unsigned;
            if (_context.State.HasFlag(ConsensusState.BlockSent)) return;
            if (body.ValidatorIndex == _context.MyIndex) return;
            if (body.Version != ConsensusContext.Version) return;
            if (body.PrevHash != _context.PreviousBlockHash || body.BlockIndex != _context.BlockIndex)
            {
                _logger.LogWarning(
                    $"Cannot handle consensus payload at height={body.BlockIndex}, " +
                    $"local height={_blockchainContext.CurrentBlock.Index}"
                );
                if (_blockchainContext.CurrentBlock.Index + 1 < body.BlockIndex)
                {
                    return;
                }

                _logger.LogWarning($"Rejected consensus payload because of prev hash mismatch");
                return;
            }

            if (body.ValidatorIndex >= _context.ValidatorCount) return;

            ConsensusPayloadCustomData data;
            try
            {
                data = ConsensusPayloadCustomData.DeserializeFrom(body.Data);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Rejected consensus payload because of deserialization error");
                _logger.LogDebug($"Exception {e} while deserializing payload={body.Data}");
                _logger.LogDebug(e.StackTrace);
                return;
            }

            if (data.ViewNumber != _context.ViewNumber && data.Type != ConsensusMessageType.ChangeView)
            {
                _logger.LogWarning(
                    $"Rejected consensus payload of type {data.Type} because view does not match, " +
                    $"my={_context.ViewNumber} theirs={data.ViewNumber}"
                );
                return;
            }

            switch (data.Type)
            {
                case ConsensusMessageType.ChangeView:
                    OnChangeViewReceived(body, (ChangeView) data);
                    break;
                case ConsensusMessageType.PrepareRequest:
                    OnPrepareRequestReceived(body, (PrepareRequest) data);
                    break;
                case ConsensusMessageType.PrepareResponse:
                    //OnPrepareResponseReceived(body, (PrepareResponse) data);
                    break;
            }
        }

        private void OnPrepareRequestReceived(ConsensusPayloadUnsigned message, PrepareRequest request)
        {
            if (_context.State.HasFlag(ConsensusState.RequestReceived))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: we are already prepared"
                );
                return;
            }

            if (message.ValidatorIndex != _context.PrimaryIndex)
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: validator is not primary"
                );
                return;
            }

            _logger.LogInformation(
                $"{nameof(OnPrepareRequestReceived)}: height={message.BlockIndex} view={request.ViewNumber} " +
                $"index={message.ValidatorIndex} tx={request.TransactionHashes.Length}"
            );
            if (!_context.State.HasFlag(ConsensusState.Backup))
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: were are primary"
                );
                return;
            }

            if (_context.Timestamp <= _blockchainContext.LastBlockHeader.Timestamp ||
                message.Timestamp > DateTime.UtcNow.AddMinutes(10).ToTimestamp())
            {
                _logger.LogDebug(
                    $"Ignoring prepare request from validator={message.ValidatorIndex}: " +
                    $"timestamp incorrect: theirs={message.Timestamp} ours={_context.Timestamp} " +
                    $"last_block={_blockchainContext.LastBlockHeader.Timestamp}"
                );
                return;
            }

            _context.State |= ConsensusState.RequestReceived;
            _context.Timestamp = message.Timestamp;
            _context.Nonce = request.Nonce; // TODO: we are blindly accepting their nonce!
            _context.CurrentProposal = new ConsensusProposal
            {
                TransactionHashes = request.TransactionHashes,
                Transactions = new Dictionary<UInt256, Transaction>()
            };


            /* TODO: check signature
            byte[] hashData = BinarySerializer.Default.Serialize(_context.GetProposedHeader().Hash);
             if (!Crypto.Default.VerifySignature(hashData, request.Signature,
                 _context.Validators[message.ValidatorIndex].PublicKey.DecodedData))
            {
                return;
            }
            for (int i = 0; i < context.Signatures.Length; i++)
                if (context.Signatures[i] != null)
                    if (!Crypto.Default.VerifySignature(hashData, context.Signatures[i],
                        context.Validators[i].EncodePoint(false)))
                        context.Signatures[i] = null;
            */
            _context.Validators[message.ValidatorIndex].BlockSignature = request.Signature;

            _context.CurrentProposal.TransactionHashes.ForEach(hash =>
            {
                Transaction transaction = _transactionPool.FindByHash(hash);
                if (transaction != null) _context.CurrentProposal.Transactions[hash] = transaction;
                else _transactionCrawler.AddTransactionHash(hash);
            });
            // TODO: catch when tx is processed
        }

        private void OnChangeViewReceived(ConsensusPayloadUnsigned messageBody, ChangeView changeView)
        {
            if (changeView.NewViewNumber <= _context.Validators[messageBody.ValidatorIndex].ExpectedViewNumber)
                return;
            _logger.LogInformation(
                $"{nameof(OnChangeViewReceived)}: height={messageBody.BlockIndex} view={changeView.ViewNumber} " +
                $"index={messageBody.ValidatorIndex} nv={changeView.NewViewNumber}"
            );
            _context.Validators[messageBody.ValidatorIndex].ExpectedViewNumber = changeView.NewViewNumber;
            CheckExpectedView(changeView.NewViewNumber);
        }

        private void CheckExpectedView(byte viewNumber)
        {
            if (_context.ViewNumber == viewNumber) return;
            if (_context.Validators.Select(v => v.ExpectedViewNumber).Count(p => p == viewNumber) >= _context.Quorum)
            {
                InitializeConsensus(viewNumber);
            }
        }

        public void InitializeConsensus(byte viewNumber)
        {
            throw new NotImplementedException();
        }
    }
}