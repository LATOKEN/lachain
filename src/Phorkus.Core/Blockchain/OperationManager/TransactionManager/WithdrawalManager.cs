using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.CrossChain;
using Phorkus.Core.Storage;
using Phorkus.Core.Threshold;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.Blockchain.OperationManager.TransactionManager
{
    public class WithdrawalManager : IWithdrawalManager
    {
        private readonly IThresholdManager _thresholdManager;
        private readonly ICrossChainManager _crossChainManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionSigner _transactionSigner;

        private readonly ILogger<IWithdrawalManager> _logger;
        private readonly ConcurrentQueue<Transaction> _withdrawalsPending = new ConcurrentQueue<Transaction>();

        private readonly ConcurrentQueue<KeyValuePair<Transaction, ITransactionData>> _withdrawalsSent =
            new ConcurrentQueue<KeyValuePair<Transaction, ITransactionData>>();


        private KeyPair _keyPair;
        private ThresholdKey _thresholdKey;
        private volatile bool _stopped = true;

        public WithdrawalManager(IThresholdManager thresholdManager, ICrossChainManager crossChainManager,
            ITransactionBuilder transactionBuilder, ITransactionPool transactionPool,
            ITransactionSigner transactionSigner, ILogger<IWithdrawalManager> logger)
        {
            _thresholdManager = thresholdManager;
            _crossChainManager = crossChainManager;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionSigner = transactionSigner;
            _logger = logger;
        }

        public void AddWithdrawal(Transaction transaction)
        {
            _withdrawalsPending.Enqueue(transaction);
        }

        private void _CommitTransactions()
        {
            while (!_stopped)
            {
                var crosschainTransaction = new KeyValuePair<Transaction, ITransactionData>();
                while (_withdrawalsSent.TryDequeue(out crosschainTransaction))
                {
                    var transaction = crosschainTransaction.Key.Withdraw;
                    var transactionService =
                        _crossChainManager.GetTransactionService(transaction.BlockchainType);
                    var transactionHash = transactionService.BroadcastTransaction(crosschainTransaction.Value);
                    if (transactionHash != null)
                    {
                        var confirmTransaction = _transactionBuilder.ConfirmTransaction(crosschainTransaction.Key.From,
                            transaction.Recipient,
                            transaction.BlockchainType, new Money(transaction.Value),
                            transactionHash, transaction.AddressFormat, transaction.Timestamp);
                        var signedTransaction = _transactionSigner.Sign(confirmTransaction, _keyPair);
                        if (!_transactionPool.Add(signedTransaction))
                        {
                            _logger.LogDebug("Couldn't send transaction " + transactionHash);
                        }
                    }
                }
            }
        }
        
        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            _thresholdKey = thresholdKey;
            _keyPair = keyPair;
            Task.Factory.StartNew(_CommitTransactions, TaskCreationOptions.LongRunning);
            while (!_stopped)
            {
                var transaction = new Transaction();
                while (_withdrawalsPending.TryDequeue(out transaction))
                {
                    var transactionFactory =
                        _crossChainManager.GetTransactionFactory(transaction.Withdraw.BlockchainType);
                    var dataToSign = transactionFactory.CreateDataToSign(transaction.From.ToByteArray(),
                        transaction.Withdraw.Recipient.ToByteArray(), transaction.Withdraw.Value.ToByteArray());
                    var signatures = new Collection<byte[]>();
                    signatures.Add(_thresholdManager.SignData(_keyPair, dataToSign.EllipticCurveType.ToString(),
                        dataToSign.DataToSign.FirstOrDefault()));

                    var rawTransaction = transactionFactory.CreateRawTransaction(transaction.From.ToByteArray(),
                        transaction.Withdraw.Recipient.ToByteArray(), transaction.Withdraw.Value.ToByteArray(),
                        signatures);
                    var transactionService =
                        _crossChainManager.GetTransactionService(transaction.Withdraw.BlockchainType);
                    _withdrawalsSent.Enqueue(
                        new KeyValuePair<Transaction, ITransactionData>(transaction, rawTransaction));
                }
            }
        }

        public void Stop()
        {
            _stopped = true;
        }
    }
}