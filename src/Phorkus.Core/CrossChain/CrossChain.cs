using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NBitcoin;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.Repositories;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.CrossChain
{
    public class CrossChain : ICrossChain
    {
        private readonly ICrossChainManager _crossChainManager = new CrossChainManager();

        private readonly ConcurrentDictionary<BlockchainType, ulong> _lastBlocks =
            new ConcurrentDictionary<BlockchainType, ulong>();

        private readonly IDictionary<BlockchainType, Timer> _synchronizeTimers
            = new Dictionary<BlockchainType, Timer>();

        private readonly IGlobalRepository _globalRepository;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionManager _transactionManager;
        private readonly ICrypto _crypto;
        private readonly IValidatorManager _validatorManager;

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
            ITransactionManager transactionManager,
            ICrypto crypto,
            IValidatorManager validatorManager)
        {
            _globalRepository = globalRepository;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
            _transactionManager = transactionManager;
            _crypto = crypto;
            _validatorManager = validatorManager;
        }

        private void _SynchronizeBlockchain(object state)
        {
            if (!(state is BlockchainType blockchainType))
                return;
            /* check blockchain's block height */
            var transactionService = _crossChainManager.GetTransactionService(blockchainType);
            var currentHeight = transactionService.CurrentBlockHeight;
            if (!_lastBlocks.TryGetValue(blockchainType, out var lastHeight) || lastHeight >= currentHeight)
                return;
            /* determine and encode our public key */
            var address = transactionService.GenerateAddress(_thresholdKey.PublicKey); 
            for (; lastHeight < currentHeight; ++lastHeight)
            {
                /* try get transactions */
                var txs = transactionService.GetTransactionsAtBlock(address, lastHeight);
                foreach (var tx in txs)
                    _CreateDepositTransaction(tx);
            }

            _lastBlocks.AddOrReplace(blockchainType, lastHeight);
        }

        private void _CreateDepositTransaction(IContractTransaction contractTransaction)
        {
            var address = _crypto.ComputeAddress(_keyPair.PublicKey.Buffer.ToByteArray()).ToUInt160();
            /* only validators can create deposit transactions */
            if (!_validatorManager.CheckValidator(_keyPair.PublicKey))
                return;
            var tx = _transactionBuilder.DepositTransaction(address,
                contractTransaction.Recipient,
                contractTransaction.BlockchainType,
                contractTransaction.Value,
                contractTransaction.TransactionHash,
                contractTransaction.AddressFormat,
                contractTransaction.Timestamp);
            /* sign deposit transaction with validator's private key and put into transaction pool */
            var signedTx = _transactionManager.Sign(tx, _keyPair);
            _transactionPool.Add(signedTx);
        }

        public void Start(ThresholdKey thresholdKey, KeyPair keyPair)
        {
            _thresholdKey = thresholdKey ?? throw new ArgumentNullException(nameof(thresholdKey));
            _keyPair = keyPair ?? throw new ArgumentNullException(nameof(keyPair));

            _lastBlocks.GetOrAdd(BlockchainType.Bitcoin,
                _crossChainManager.GetTransactionService(BlockchainType.Bitcoin).CurrentBlockHeight);
            _lastBlocks.GetOrAdd(BlockchainType.Ethereum,
                _crossChainManager.GetTransactionService(BlockchainType.Ethereum).CurrentBlockHeight);
            
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