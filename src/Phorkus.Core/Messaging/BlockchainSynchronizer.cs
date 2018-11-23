using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Logging;
using Phorkus.Core.Network;
using Phorkus.Core.Utils;
using Phorkus.Proto;

namespace Phorkus.Core.Messaging
{
    public class BlockchainSynchronizer : IBlockchainSynchronizer
    {
        private readonly ITransactionPool _transactionPool;
        private readonly INetworkContext _networkContext;
        private readonly IBlockchainContext _blockchainContext;
        private readonly IMessageFactory _messageFactory;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionManager _transactionManager;
        private readonly ILogger<IBlockchainSynchronizer> _logger;
        private readonly ITransactionVerifier _transactionVerifier;
        private readonly IBroadcaster _broadcaster;

        public BlockchainSynchronizer(
            ITransactionPool transactionPool,
            INetworkContext networkContext,
            IBlockchainContext blockchainContext,
            IMessageFactory messageFactory,
            IBlockManager blockManager,
            ITransactionManager transactionManager,
            ILogger<IBlockchainSynchronizer> logger,
            ITransactionVerifier transactionVerifier,
            IBroadcaster broadcaster)
        {
            _transactionPool = transactionPool;
            _networkContext = networkContext;
            _blockchainContext = blockchainContext;
            _messageFactory = messageFactory;
            _blockManager = blockManager;
            _transactionManager = transactionManager;
            _logger = logger;
            _transactionVerifier = transactionVerifier;
            _broadcaster = broadcaster;
        }

        private readonly object _transactionsGot = new object();

        public uint WaitForTransactions(IEnumerable<UInt256> transactionHashes, TimeSpan timeout)
        {
            var txHashes = transactionHashes as UInt256[] ?? transactionHashes.ToArray();
            var lostTxs = _HaveTransactions(txHashes);
            if (!lostTxs.Any())
                return (uint) txHashes.Length;
            _broadcaster.Broadcast(_messageFactory.GetTransactionsMessage(lostTxs));
            lock (_transactionsGot)
            {
                /* TODO: "possible error if one peer doesn't have one of transactions requested" */
                lostTxs = _HaveTransactions(txHashes);
                if (!lostTxs.Any())
                    return (uint) txHashes.Length;
                Monitor.Wait(_transactionsGot, timeout);
                lostTxs = _HaveTransactions(txHashes);
            }
            return (uint)(txHashes.Length - lostTxs.Count);
        }
        
        public void HandleTransactionsFromPeer(IEnumerable<SignedTransaction> transactions, IPeer peer)
        {
            foreach (var tx in transactions)
            {
                if (!_transactionVerifier.VerifyTransactionImmediately(tx))
                    continue;
                try
                {
                    _transactionManager.Persist(tx);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Unable to persist transaction {tx.Hash.Buffer.ToBase58()}: {e}");
                }
            }

            lock (_transactionsGot)
            {
                Monitor.PulseAll(_transactionsGot);
            }
        }

        public void HandleBlockFromPeer(Block block, IPeer peer)
        {
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
            if (block.Header.Index <= myHeight)
                return;

            var haveNotTxs = _HaveTransactions(block);

            if (haveNotTxs.Count > 0)
            {
                _broadcaster.Broadcast(_messageFactory.GetTransactionsMessage(haveNotTxs));
                return;
            }

            var error = _blockManager.Persist(block);
            if (error == OperatingError.BlockAlreadyExists)
                return;
            if (error != OperatingError.Ok)
            {
                _logger.LogWarning($"Unable to persist block {block.Header.Index}, got error {error}, dropping peer");
                return;
            }
            _logger.LogInformation($"Synchronized block {block.Header.Index} with hash {block.Hash}");
        }

        private List<UInt256> _HaveTransactions(IEnumerable<UInt256> txHashes)
        {
            var list = new List<UInt256>();
            foreach (var hash in txHashes)
            {
                var tx = _transactionManager.GetByHash(hash);
                if (tx != null)
                    continue;
                list.Add(hash);
            }

            return list;
        }

        private List<UInt256> _HaveTransactions(Block block)
        {
            return _HaveTransactions(block.Header.TransactionHashes);
        }

        private ulong _currentMaximumHeight;

        private void _SynchronizerWorker()
        {
            var myHeight = _blockchainContext.CurrentBlockHeaderHeight;
            if (myHeight > _networkContext.LocalNode.BlockHeight)
            {
                _networkContext.LocalNode.BlockHeight = myHeight;
                _broadcaster.Broadcast(_messageFactory.HandshakeResponse(_networkContext.LocalNode));
            }
            
            Thread.Sleep(1000);

            var activePeers = _networkContext.ActivePeers.Values
                .Where(peer => peer.Node != null && peer.IsKnown && peer.IsConnected);
            var arrayOfPeers = activePeers as IPeer[] ?? activePeers.ToArray();
            if (!arrayOfPeers.Any())
                return;
            var maxHeight = arrayOfPeers.Select(peer => peer.Node.BlockHeight).Max();
            if (maxHeight > _currentMaximumHeight)
                _currentMaximumHeight = maxHeight;

            Thread.Sleep(1000);
            
            if (_blockchainContext.CurrentBlockHeaderHeight >= _currentMaximumHeight)
                return;
            _broadcaster.Broadcast(_messageFactory.GetBlocksMessage(myHeight));
        }

        public void Start()
        {
            Task.Factory.StartNew(() =>
            {
                var thread = Thread.CurrentThread;
                while (thread.IsAlive)
                {
                    try
                    {
                        _SynchronizerWorker();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }
    }
}