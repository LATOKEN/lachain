using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Contracts.MessageEncodingServices;
using Nethereum.Util;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Storage;
using Phorkus.Core.Utils;
using Phorkus.CrossChain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.CrossChain
{
    public class CrossChain : ICrossChain
    {
        private readonly ICrossChainManager _crossChainManager = new CrossChainManager();

        private readonly IDictionary<BlockchainType, Timer> _synchronizeTimers
            = new Dictionary<BlockchainType, Timer>();

        private readonly IGlobalRepository _globalRepository;
        private readonly ILogger<ICrossChain> _logger;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionPool _transactionPool;

        private readonly BlockchainType[] _blockchainTypes =
        {
            BlockchainType.Bitcoin,
            BlockchainType.Ethereum
        };

        public bool IsWorking { get; set; }

        public CrossChain(
            IGlobalRepository globalRepository,
            ILogger<ICrossChain> logger,
            ITransactionBuilder transactionBuilder,
            ITransactionPool transactionPool)
        {
            _globalRepository = globalRepository;
            _logger = logger;
            _transactionBuilder = transactionBuilder;
            _transactionPool = transactionPool;
        }

        private void _Worker()
        {
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
            var thresholdKey = _globalRepository.GetShare();
            if (thresholdKey is null)
                throw new Exception(
                    "You can't fetch transactions from other blockchains, cuz you don't have private share");
            var rawPublicKey = thresholdKey.PublicKey.Buffer.ToByteArray();
            var address = _EncodeAddress(transactionService.AddressFormat, rawPublicKey);
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
            /* TODO: "cross-chain module should return UInt160 value as from" */
            var recipient = contractTransaction.From.ToUInt160();
            var tx = _transactionBuilder.DepositTransaction(recipient, contractTransaction.BlockchainType, contractTransaction.Value,
                contractTransaction.AddressFormat, contractTransaction.Timestamp);
            /* TODO: "sign transaction here and put into transaction pool" */
        }

        private byte[] _EncodeAddress(AddressFormat addressFormat, byte[] publicKey)
        {
            switch (addressFormat)
            {
                case AddressFormat.Ripmd160:
                    return publicKey.Ripemd160();
                case AddressFormat.Ed25519:
                    return publicKey.Ed25519();
                default:
                    throw new ArgumentOutOfRangeException(nameof(addressFormat), addressFormat, null);
            }
        }

        public void Start()
        {
            var thresholdKey = _globalRepository.GetShare();
            if (thresholdKey is null)
                throw new Exception(
                    "You can't fetch transactions from other blockchains, cuz you don't have private share");

            foreach (var blockchainType in _blockchainTypes)
            {
                if (_synchronizeTimers.ContainsKey(blockchainType))
                    continue;
                var transactionService = _crossChainManager.GetTransactionService(blockchainType);
                var blockGenerationTime = (int) transactionService.BlockGenerationTime;
                _synchronizeTimers.Add(blockchainType,
                    new Timer(_SynchronizeBlockchain, blockchainType, blockGenerationTime, blockGenerationTime));
            }

            Task.Factory.StartNew(() =>
            {
                while (IsWorking)
                {
                    try
                    {
                        _Worker();
                    }
                    catch (Exception e)
                    {
                        _logger.LogError($"Cross chain worker has failed: {e}");
                        Thread.Sleep(3000);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void Stop()
        {
            foreach (var timer in _synchronizeTimers)
                timer.Value.Dispose();
            _synchronizeTimers.Clear();
            IsWorking = false;
        }
    }
}