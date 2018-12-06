using System;
using System.Collections.Generic;
using System.Threading;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.CrossChain
{
    public class CrossChain : ICrossChain
    {
        private readonly ICrossChainManager _crossChainManager = new CrossChainManager();

        private readonly IDictionary<BlockchainType, Timer> _synchronizeTimers
            = new Dictionary<BlockchainType, Timer>();

        private readonly IGlobalRepository _globalRepository;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionManager _transactionManager;

        private readonly BlockchainType[] _blockchainTypes =
        {
            BlockchainType.Bitcoin,
            BlockchainType.Ethereum
        };

        private ThresholdKey _thresholdKey;
        private KeyPair _keyPair;

        public bool IsWorking { get; set; }

        public CrossChain(
            IGlobalRepository globalRepository,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool,
            ITransactionManager transactionManager)
        {
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
        }

        private void _SynchronizeBlockchain(object state)
        {
            if (!(state is BlockchainType blockchainType))
                return;
            var transactionService = _crossChainManager.GetTransactionService(blockchainType);
            /* check blockchain's block height */
            var currentHeight = _globalRepository.GetBlockchainHeight(blockchainType);
            var blockchainHeight = transactionService.CurrentBlockHeight;
            if (currentHeight >= blockchainHeight)
                return;
            /* determine and encode our public key */
            var rawPublicKey = _thresholdKey.PublicKey.Buffer.ToByteArray();
            var address = AddressEncoder.EncodeAddress(transactionService.AddressFormat, rawPublicKey);
            /* try get transactions */
            for (var height = currentHeight; height <= blockchainHeight; height++)
            {
                var txs = transactionService.GetTransactionsAtBlock(address, height);
                foreach (var tx in txs)
                    _CreateDepositTransaction(tx);
            }
        }
        
        private void _CreateDepositTransaction(IContractTransaction contractTransaction)
        {
            var tx = _transactionBuilder.DepositTransaction(
                contractTransaction.From,
                contractTransaction.BlockchainType,
                contractTransaction.Value,
                contractTransaction.TransactionHash,
                contractTransaction.AddressFormat,
                contractTransaction.Timestamp);
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
        }
        
        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            _thresholdKey = thresholdKey ?? throw new ArgumentNullException(nameof(thresholdKey));
            _keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));

            foreach (var blockchainType in _blockchainTypes)
            {
                if (_synchronizeTimers.ContainsKey(blockchainType))
                    continue;
                var transactionService = _crossChainManager.GetTransactionService(blockchainType);
                var blockGenerationTime = (int) transactionService.BlockGenerationTime;
                _synchronizeTimers.Add(blockchainType,
                    new Timer(_SynchronizeBlockchain, blockchainType, blockGenerationTime, blockGenerationTime));
            }
        }

        public void Stop()
        {
            foreach (var timer in _synchronizeTimers)
                timer.Value.Dispose();
            _synchronizeTimers.Clear();
            IsWorking = false;
            _thresholdKey = null;
            _keyPair = null;
        }
    }
}