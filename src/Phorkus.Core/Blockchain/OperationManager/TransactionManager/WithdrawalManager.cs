using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
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
        private readonly IWithdrawalRepository _withdrawalRepository;
        private readonly ILogger<IWithdrawalManager> _logger;
        private volatile bool _stopped = true;
        
        public ulong CurrentNoncePending { get; set; }
        public ulong CurrentNonceSent { get; set; }

        public WithdrawalManager(
            ICrossChainManager crossChainManager,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            ITransactionRepository transactionRepository,
            IValidatorManager validatorManager,
            IThresholdManager thresholdManager,
            IWithdrawalRepository withdrawalRepository,
            ILogger<IWithdrawalManager> logger)
        {
            _crossChainManager = crossChainManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionRepository = transactionRepository;
            _transactionSigner = transactionSigner;
            _thresholdManager = thresholdManager;
            _validatorManager = validatorManager;
            _withdrawalRepository = withdrawalRepository;
            _logger = logger;
        }

        public OperatingError Verify(Transaction transaction)
        {
            if (transaction.Version != 0)
                return OperatingError.UnsupportedVersion;
            if (transaction.Type != TransactionType.Withdraw)
                return OperatingError.InvalidTransaction;
            var confirm = transaction.Deposit;
            if (confirm?.BlockchainType is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.TransactionHash is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Timestamp is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.AddressFormat is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Recipient is null)
                return OperatingError.InvalidTransaction;
            if (confirm?.Value is null)
                return OperatingError.InvalidTransaction;
            if (!_validatorManager.CheckValidator(transaction.From))
                return OperatingError.InvalidTransaction;
            return OperatingError.Ok;
            // throw new OperationNotSupportedException();
        }
        
        
        public OperatingError Verify(Withdrawal withdrawal)
        {
            return _IsTxValid(withdrawal.TransactionHash);
        }


        public Withdrawal CreateWithdrawal(Transaction transaction)
        {
            var result = _IsTxValid(transaction);
            if (result != OperatingError.Ok)
                throw new InvalidTransactionException(result);
            var withdrawal = new Withdrawal
            {
                TransactionHash = transaction.ToHash256(),
                OriginalHash = transaction.Withdraw.TransactionHash,
                Nonce = CurrentNoncePending,
                State = WithdrawalState.Registered,
                Timestamp = (ulong) new DateTimeOffset().ToUnixTimeMilliseconds()
            };
            _withdrawalRepository.AddWithdrawal(withdrawal);
            _withdrawalRepository.AddWithdrawalState(withdrawal);
            ++CurrentNoncePending;
            return withdrawal;
        }

        public OperatingError Verify(Withdrawal withdrawal, WithdrawalState withdrawalState)
        {
            if (withdrawal.State != withdrawalState)
            {
                return OperatingError.InvalidState;
            }
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
                    $"Unable     to process non-withdrawal transaction ({tx.ToHash256()}), got type ({tx.Type})");
                return OperatingError.InvalidTransaction;
            }

            var withdrawTx = tx.Withdraw;
            return withdrawTx is null ? OperatingError.InvalidTransaction : OperatingError.Ok;
        }

        public void ConfirmWithdrawal(KeyPair keyPair, ulong nonce)
        {
            var withdrawal = _withdrawalRepository.GetWithdrawalByStateNonce(WithdrawalState.Sent, nonce);
            var result = Verify(withdrawal, WithdrawalState.Sent);
            if (result != OperatingError.Ok)
                throw new InvalidTransactionException(result);
            var transaction = _transactionRepository.GetTransactionByHash(withdrawal.TransactionHash)?.Transaction;
            /* this should never happens */
            if (transaction is null)
                throw new InvalidTransactionException(OperatingError.InvalidTransaction);
            
            /* check transaction confirmation in blockchain */
            var transactionService = _crossChainManager.GetTransactionService(transaction.Withdraw.BlockchainType);
            var awaitTime = withdrawal.Timestamp + transactionService.BlockGenerationTime * transactionService.TxConfirmation;
            Thread.Sleep(TimeSpan.FromMilliseconds(new DateTimeOffset().ToUnixTimeMilliseconds() - (long) awaitTime));
            if (!transactionService.CheckTransactionIsConfirmed(withdrawal.OriginalHash.ToByteArray()))
            {
                return;
            }
            
            /* create confirm transaction */
            var withdrawTx = transaction.Withdraw;
            var confirmTransaction = _transactionBuilder.ConfirmTransaction(
                transaction.From,
                withdrawTx.Recipient,
                withdrawTx.BlockchainType,
                new Money(withdrawTx.Value),
                withdrawTx.TransactionHash.ToByteArray(),
                withdrawTx.AddressFormat,
                withdrawTx.Timestamp);
            /* only validators can sign confirm transactions */
            if (!_validatorManager.CheckValidator(keyPair.PublicKey))
                return;
            /* sign transaction with validator's private key */
            var signedTransaction = _transactionSigner.Sign(confirmTransaction, keyPair);
            if (Verify(signedTransaction.Transaction) != OperatingError.Ok || !_transactionPool.Add(signedTransaction))
            {
                _logger.LogDebug(
                    $"Couldn't send confirm transaction transaction ({withdrawTx.TransactionHash.ToByteArray()}) to transaction pool");
            }
            else
            {
                _withdrawalRepository.ChangeWithdrawalState(withdrawal.TransactionHash, WithdrawalState.Approved);
            }
        }

        public void ExecuteWithdrawal(ThresholdKey thresholdKey, KeyPair keyPair, ulong nonce)
        {
            var withdrawal = _withdrawalRepository.GetWithdrawalByStateNonce(WithdrawalState.Registered, nonce);
            var result = Verify(withdrawal, WithdrawalState.Registered);
            if (result != OperatingError.Ok)
                return;

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
            foreach (var data in dataToSign)
            {
                var signature =
                    _thresholdManager.SignData(keyPair, data.EllipticCurveType.ToString(), data.TransactionHash);
                if (signature is null)
                {
                    _logger.LogError($"Unable to sign data for withdraw ({withdrawal.TransactionHash})");
                    continue;
                }

                signatures.Add(signature);
            }

            var rawTransaction = transactionFactory.CreateRawTransaction(transaction.From.ToByteArray(),
                transaction.Withdraw.Recipient.ToByteArray(), transaction.Withdraw.Value.ToByteArray(),
                signatures);
            
            var transactionHash = transactionService.BroadcastTransaction(rawTransaction);
            if (transactionHash == null)
                return;
            withdrawal.Timestamp = (ulong) new DateTimeOffset().ToUnixTimeMilliseconds();
            withdrawal.OriginalTransaction = ByteString.CopyFrom(rawTransaction.TransactionData);
            withdrawal.OriginalHash = ByteString.CopyFrom(transactionHash);
            withdrawal.State = WithdrawalState.Sent;
            withdrawal.Nonce = CurrentNonceSent;

            _withdrawalRepository.ChangeWithdrawal(withdrawal.TransactionHash, withdrawal);
            _withdrawalRepository.AddWithdrawalState(withdrawal);
            ++CurrentNonceSent;
        }
    }
}