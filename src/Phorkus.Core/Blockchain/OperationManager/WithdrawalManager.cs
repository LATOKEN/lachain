using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Core.Threshold;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.Blockchain.OperationManager
{
    public class WithdrawalManager : IWithdrawalManager
    {
        private readonly ICrossChainManager _crossChainManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;
        private readonly IPoolRepository _poolRepository;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IValidatorManager _validatorManager;
        private readonly IStateManager _stateManager;
        private readonly IThresholdManager _thresholdManager;
        private readonly ILogger<IWithdrawalManager> _logger;

        private volatile bool _stopped = true;

        public WithdrawalManager(
            ICrossChainManager crossChainManager,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionSigner transactionSigner,
            IPoolRepository poolRepository,
            IValidatorManager validatorManager,
            IStateManager stateManager,
            IThresholdManager thresholdManager,
            ILogger<IWithdrawalManager> logger)
        {
            _crossChainManager = crossChainManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _poolRepository = poolRepository;
            _transactionSigner = transactionSigner;
            _thresholdManager = thresholdManager;
            _stateManager = stateManager;
            _validatorManager = validatorManager;
            _logger = logger;
            _stateManager = stateManager;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryApproveWithdrawal(KeyPair keyPair, ulong nonce)
        {
            var withdrawal = _stateManager.LastApprovedSnapshot.Withdrawals.GetWithdrawalByNonce(nonce);
            if (withdrawal is null)
            {
                _logger.LogWarning($"Unable to find withdrawal by nonce ({nonce}) for execution");
                return false;
            }

            if (withdrawal.State != WithdrawalState.Sent)
            {
                _logger.LogWarning($"You can't execute withdrawals with not sent state, found ({withdrawal.State})");
                return false;
            }

            /* try to fetch transaction from database storage */
            var transaction = _poolRepository.GetTransactionByHash(withdrawal.TransactionHash)?.Transaction;
            if (transaction is null)
                throw new InvalidTransactionException(OperatingError.InvalidTransaction);
            /* check transaction confirmation in blockchain */
            var transactionService = _crossChainManager.GetTransactionService(transaction.Withdraw.BlockchainType);
            if (!transactionService.IsTransactionConfirmed(withdrawal.OriginalHash.ToByteArray()))
                return false;

            /* TODO: "stop! you can't wait here for block confirmation!" 
            var awaitTime = withdrawal.Timestamp + transactionService.BlockGenerationTime * transactionService.TxConfirmation;
            Thread.Sleep(TimeSpan.FromMilliseconds(new DateTimeOffset().ToUnixTimeMilliseconds() - (long) awaitTime));*.
            
            /* create confirm transaction */
            var withdrawTx = transaction.Withdraw;
            var confirmTransaction = _transactionBuilder.ConfirmTransaction(
                transaction.From,
                withdrawTx.Recipient,
                withdrawTx.BlockchainType,
                withdrawTx.Value.ToMoney(),
                withdrawTx.TransactionHash.ToByteArray(),
                withdrawTx.AddressFormat,
                withdrawTx.Timestamp);
            /* only validators can sign confirm transactions */
            if (!_validatorManager.CheckValidator(keyPair.PublicKey))
                return false;
            /* sign transaction with validator's private key */
            var acceptedTransaction = _transactionSigner.Sign(confirmTransaction, keyPair);
            if (!_transactionPool.Add(acceptedTransaction))
            {
                _logger.LogWarning(
                    $"Couldn't send confirm transaction transaction ({withdrawTx.TransactionHash.ToByteArray()}) to transaction pool");
                return false;
            }

            throw new NotImplementedException("We can't confirm withdrawal here, cuz we need to create new transaction or create system contract for cross-chain");
            /*_withdrawalRepository.Withdrawals.ApproveWithdrawal(
                withdrawal.TransactionHash);*/
            
            return true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ExecuteWithdrawal(ThresholdKey thresholdKey, KeyPair keyPair, ulong nonce)
        {
            var withdrawal = _stateManager.LastApprovedSnapshot.Withdrawals.GetWithdrawalByNonce(nonce);
            if (withdrawal is null)
            {
                _logger.LogWarning($"Unable to find withdrawal by nonce ({nonce}) for execution");
                return;
            }

            if (withdrawal.State != WithdrawalState.Registered)
            {
                _logger.LogWarning(
                    $"You can't execute withdrawals with not registered state, found ({withdrawal.State})");
                return;
            }

            var transaction = _poolRepository.GetTransactionByHash(withdrawal.TransactionHash)
                .Transaction;
            var withdrawTx = transaction.Withdraw;

            var transactionFactory = _crossChainManager.GetTransactionFactory(transaction.Withdraw.BlockchainType);
            var transactionService = _crossChainManager.GetTransactionService(transaction.Withdraw.BlockchainType);

            var publicAddress = transactionService.GenerateAddress(thresholdKey.PublicKey);
            var dataToSign = transactionFactory.CreateDataToSign(publicAddress, withdrawTx.Recipient.ToByteArray(),
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

            var rawTransaction = transactionFactory.CreateRawTransaction(
                transaction.From.ToByteArray(),
                transaction.Withdraw.Recipient.ToByteArray(),
                transaction.Withdraw.Value.ToByteArray(),
                signatures);

            var transactionHash = transactionService.BroadcastTransaction(rawTransaction);
            if (transactionHash == null)
                return;
            
            throw new NotImplementedException("We can't confirm withdrawal here, cuz we need to create new transaction or create system contract for cross-chain");
            /*_withdrawalRepository.ConfirmWithdrawal(
                withdrawal.TransactionHash,
                rawTransaction.TransactionData,
                transactionHash);*/
        }

        private void _ExecuteWorker(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            while (!_stopped)
            {
                Thread.Sleep(1_000);

                var approved = _stateManager.LastApprovedSnapshot.Withdrawals.GetApprovedWithdrawalNonce();
                var current = _stateManager.LastApprovedSnapshot.Withdrawals.GetCurrentWithdrawalNonce();

                if (current >= approved)
                    continue;

                for (var i = approved; i <= current; i++)
                    ExecuteWithdrawal(thresholdKey, keyPair, i);
            }
        }

        private void _ConfirmWorker(KeyPair keyPair)
        {
            while (!_stopped)
            {
                Thread.Sleep(10_000);

                var approved = _stateManager.LastApprovedSnapshot.Withdrawals.GetApprovedWithdrawalNonce();
                var current = _stateManager.LastApprovedSnapshot.Withdrawals.GetCurrentWithdrawalNonce();

                if (current >= approved)
                    continue;

                TryApproveWithdrawal(keyPair, current);
            }
        }

        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            if (thresholdKey is null)
                throw new ArgumentNullException(nameof(thresholdKey));
            if (keyPair is null)
                throw new ArgumentNullException(nameof(keyPair));
            
            Task.Factory.StartNew(() =>
            {
                try
                {
                    _ExecuteWorker(thresholdKey, keyPair);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed to start withdrawal manager: {e}");
                }
            }, TaskCreationOptions.LongRunning);

            Task.Factory.StartNew(() =>
            {
                try
                {
                    _ConfirmWorker(keyPair);
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