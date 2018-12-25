using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Google.Protobuf;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Storage.RocksDB.Repositories;

namespace Phorkus.Core.Blockchain
{
    public class WithdrawalRunner : IWithdrawalRunner
    {
        private readonly ILogger<IWithdrawalRunner> _logger;
        private readonly IWithdrawalManager _withdrawalManager;
        private volatile bool _stopped = true;
        private ulong noncePending = 0;
        private ulong nonceSent = 0;

        public WithdrawalRunner(IWithdrawalManager withdrawalManager, ILogger<IWithdrawalRunner> logger,
            IWithdrawalRepository withdrawalRepository)
        {
            _logger = logger;
            _withdrawalManager = withdrawalManager;
        }

        private void _ExecuteWithdrawals(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            while (!_stopped)
            {
                var lastNoncePending = _withdrawalManager.CurrentNoncePending;
                for (; noncePending < lastNoncePending; ++noncePending)
                {
                    _withdrawalManager.ExecuteWithdrawal(thresholdKey, keyPair, noncePending);
                }
            }
        }

        private void _ConfirmWithdrawals(KeyPair keyPair)
        {
            while (!_stopped)
            {
                var lastNonceSent = _withdrawalManager.CurrentNonceSent;
                for (; nonceSent < lastNonceSent; ++nonceSent)
                {
                    _withdrawalManager.ConfirmWithdrawal(keyPair, nonceSent);
                }
            }
        }

        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            Task.Factory.StartNew(() =>
            {
                try
                {
                    _ExecuteWithdrawals(thresholdKey, keyPair);
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
                    _ConfirmWithdrawals(keyPair);
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