using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Core.Storage;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawalManager : IWithdrawalManager
    {
        private readonly ICrossChainManager _crossChainManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionRepository _transactionRepository;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IValidatorManager _validatorManager;
        private readonly IThresholdManager _thresholdManager;

        private readonly ILogger<IWithdrawalManager> _logger;
        private readonly ConcurrentQueue<Transaction> _withdrawalsPending = new ConcurrentQueue<Transaction>();

        /* TODO: "replace this queue with RocksDB storage" */
        private readonly ConcurrentQueue<Withdrawal> _withdrawalsQueue =
            new ConcurrentQueue<Withdrawal>();

        private volatile bool _stopped = true;

        public WithdrawalManager(
            ICrossChainManager crossChainManager,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ITransactionRepository transactionRepository,
            IValidatorManager validatorManager,
            IThresholdManager thresholdManager,
            ILogger<IWithdrawalManager> logger)
        {
            _crossChainManager = crossChainManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionRepository = transactionRepository;
            _transactionSigner = transactionSigner;
            _thresholdManager = thresholdManager;
            _validatorManager = validatorManager;
            _logger = logger;
        }

        public Withdrawal CreateWithdrawal(Transaction transaction)
        {
            var result = _IsTxValid(transaction);
            if (result != OperatingError.Ok)
                throw new InvalidTransactionException(result);
            var withdrawal = new Withdrawal
            {
                TransactionHash = transaction.ToHash256(),
                State = WithdrawalState.Registered
            };
            /* TODO: "write withdrawal here to RocksDB storage instead of local queue" */
            _withdrawalsPending.Enqueue(transaction);
            return withdrawal;
        }

        public OperatingError Verify(Withdrawal withdrawal)
        {
            return _IsTxValid(withdrawal.TransactionHash);
        }

        private OperatingError _IsTxValid(UInt256 txHash)
        {
            var tx = _transactionRepository.GetTransactionByHash(txHash)
                ?.Transaction;
            return tx is null ? OperatingError.InvalidTransaction : _IsTxValid(tx);
        }

        private OperatingError _IsTxValid(Transaction tx)
        {
            if (tx.Type != TransactionType.Withdraw)
            {
                _logger.LogWarning(
                    $"Unable to process non-withdrawal transaction ({tx.ToHash256()}), got type ({tx.Type})");
                return OperatingError.InvalidTransaction;
            }

            var withdrawTx = tx.Withdraw;
            return withdrawTx is null ? OperatingError.InvalidTransaction : OperatingError.Ok;
        }

        public void ConfirmWithdrawal(Withdrawal withdrawal, byte[] transactionHash, KeyPair keyPair)
        {
            var result = Verify(withdrawal);
            if (result != OperatingError.Ok)
                throw new InvalidTransactionException(result);
            var transaction = _transactionRepository.GetTransactionByHash(withdrawal.TransactionHash)?.Transaction;
            /* this should never happens */
            if (transaction is null)
                throw new InvalidTransactionException(OperatingError.InvalidTransaction);
            /* chjeck withdrawal states */
            if (withdrawal.State != WithdrawalState.Sent)
            {
                _logger.LogWarning($"You can't confirm not sent withdrawal ({withdrawal.TransactionHash})");
                return;
            }

            /* create confirm transaction */
            var withdrawTx = transaction.Withdraw;
            var confirmTransaction = _transactionBuilder.ConfirmTransaction(
                transaction.From,
                withdrawTx.Recipient,
                withdrawTx.BlockchainType,
                new Money(withdrawTx.Value),
                transactionHash,
                withdrawTx.AddressFormat,
                withdrawTx.Timestamp);
            /* only validators can sign confirm transactions */
            if (!_validatorManager.CheckValidator(keyPair.PublicKey))
                return;
            /* sign transaction with validator's private key */
            var signedTransaction = _transactionSigner.Sign(confirmTransaction, keyPair);
            if (!_transactionPool.Add(signedTransaction))
            {
                _logger.LogDebug(
                    $"Couldn't send confirm transaction transaction ({transactionHash}) to transaction pool");
            }
        }
        
        private void _SignWorker(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            while (!_stopped && _withdrawalsQueue.TryDequeue(out var withdrawal))
            {
                var result = Verify(withdrawal);
                if (result != OperatingError.Ok)
                    continue;
                
                var transaction = _transactionRepository.GetTransactionByHash(withdrawal.TransactionHash)
                    .Transaction;
                var withdrawTx = transaction.Withdraw;

                var transactionFactory = _crossChainManager.GetTransactionFactory(transaction.Withdraw.BlockchainType);
                var transactionService = _crossChainManager.GetTransactionService(transaction.Withdraw.BlockchainType);

                var publicAddress = transactionService.GenerateAddress(thresholdKey.PublicKey);
                
                var dataToSign = transactionFactory.CreateDataToSign(publicAddress,
                    withdrawTx.Recipient.ToByteArray(),
                    withdrawTx.Value.ToByteArray());
                
                var signatures = new List<byte[]>();
                foreach (var d in dataToSign)
                {
                    var sig = _thresholdManager.SignData(keyPair, d.EllipticCurveType.ToString(), d.TransactionHash);
                    if (sig is null)
                    {
                        _logger.LogError($"Unable to sign data for withdraw ({withdrawal.TransactionHash})");
                        continue;
                    }
                    signatures.Add(sig);
                }
                
                var rawTransaction = transactionFactory.CreateRawTransaction(transaction.From.ToByteArray(),
                    transaction.Withdraw.Recipient.ToByteArray(), transaction.Withdraw.Value.ToByteArray(),
                    signatures);
                
                var transactionHash = transactionService.BroadcastTransaction(rawTransaction);
                if (transactionHash == null)
                    continue;

                withdrawal.OriginalTransaction = ByteString.CopyFrom(rawTransaction.TransactionData);
                withdrawal.OriginalHash = ByteString.CopyFrom(transactionHash);
                
                /* TODO: "save withdrawal with new SENT state here" */
            }
        }

        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    _SignWorker(thresholdKey, keyPair);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to start withdrawal manager: {e}");
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            _stopped = true;
        }
    }
}