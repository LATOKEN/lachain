using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Lachain.Core.Blockchain.Error;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.SystemContracts.Interface;
using Lachain.Core.Blockchain.VM;
using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Logger;
using Lachain.Networking;
using Lachain.Networking.PeerFault;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Network
{
    public class BannedPeerTracker : IBannedPeerTracker
    {
        // Scans for banned peer in system contract tx
        // If a peer is banned locally, then sends a system contract tx to broadcast
        private static readonly ILogger<BannedPeerTracker> Logger = LoggerFactory.GetLoggerForClass<BannedPeerTracker>();
        private readonly IPeerBanManager _banManager;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionSigner _transactionSigner;
        private readonly IPrivateWallet _privateWallet;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionPool _transactionPool;
        private readonly IPeerBanRepository _repostiroy;
        private readonly Thread _banRequestWorker;
        private readonly Queue<InvocationContext> _banPeerRequestTx = new Queue<InvocationContext>();
        private readonly ConcurrentDictionary<ECDSAPublicKey, uint> _banPeerVotes
            = new ConcurrentDictionary<ECDSAPublicKey, uint>();
        private ISet<ECDSAPublicKey> _validators = new HashSet<ECDSAPublicKey>();
        private ulong _cycle = 0;
        private bool _running = false;
        public uint ThresholdForBan => (uint) (_validators.Count - 1) / 3;
        public BannedPeerTracker(
            IConfigManager configManager,
            IPeerBanRepository repository,
            IPeerBanManager banManager,
            ITransactionBuilder transactionBuilder,
            IPrivateWallet privateWallet,
            ITransactionSigner transactionSigner,
            IBlockManager blockManager,
            ITransactionPool transactionPool
        )
        {
            _repostiroy = repository;
            _banManager = banManager;
            _transactionBuilder = transactionBuilder;
            _privateWallet = privateWallet;
            _transactionSigner = transactionSigner;
            _blockManager = blockManager;
            _transactionPool = transactionPool;
            _banManager.OnPeerBanned += OnPeerBanned;
            _blockManager.OnBlockPersisted += OnBlockPersisted;
            _blockManager.OnSystemContractInvoked += OnSystemContractInvoked;
            _banRequestWorker = new Thread(BanRequestWorker);
            var genesisConfig = configManager.GetConfig<GenesisConfig>("genesis") ??
                throw new InvalidOperationException("No genesis config found");
            _validators = genesisConfig.Validators
                .Select(x => x.EcdsaPublicKey.HexToBytes().ToPublicKey()).ToHashSet();
        }

        private void RestoreState()
        {
            _cycle = _repostiroy.GetLowestCycleForVote();
            var votedPeers = _repostiroy.GetVotedPeers(_cycle);
            for (int i = 0 ; i < votedPeers.Length ; i += CryptoUtils.PublicKeyLength)
            {
                var publicKey = votedPeers.Skip(i).Take(CryptoUtils.PublicKeyLength).ToArray();
                var count = (uint) _repostiroy.GetVotersForBannedPeer(_cycle, publicKey).Length / CryptoUtils.PublicKeyLength;
                if (!_banPeerVotes.TryAdd(publicKey.ToPublicKey(), count))
                {
                    throw new Exception($"Could not add {publicKey.ToHex()} with votes count {count} to dictionary");
                }
                if (count > ThresholdForBan)
                {
                    _banManager.BanPeer(publicKey);
                }
            }
        }

        public void Start()
        {
            if (_running)
                return;
            if (_blockManager.GetHeight() > 0)
                _validators = _blockManager.GetByHeight(_blockManager.GetHeight())!.Multisig.Validators.ToHashSet();
            RestoreState();
            _running = true;
            _banRequestWorker.Start();
        }

        private void OnBlockPersisted(object? sender, Block block)
        {
            lock (_banPeerRequestTx)
            {
                var height = block.Header.Index;
                var currentCycle = NetworkManagerBase.CycleNumber(height);
                if (NetworkManagerBase.BlockInCycle(height) == 0 && _cycle < currentCycle)
                {
                    ClearVotes();
                    _banPeerRequestTx.Clear();
                    _validators = block.Multisig.Validators.ToHashSet();
                }
                _cycle = currentCycle;
                Monitor.PulseAll(_banPeerRequestTx);
            }
        }

        private void OnSystemContractInvoked(object? sender, InvocationContext context)
        {
            lock (_banPeerRequestTx)
            {
                var height = context.Receipt.Block;
                var currentCycle = NetworkManagerBase.CycleNumber(height);
                if (NetworkManagerBase.BlockInCycle(height) == 0 && _cycle < currentCycle)
                {
                    ClearVotes();
                    _banPeerRequestTx.Clear();
                }
                _cycle = currentCycle;
                _banPeerRequestTx.Enqueue(context);
                Monitor.PulseAll(_banPeerRequestTx);
            }
        }

        private void BanRequestWorker()
        {
            Logger.LogTrace("Starting BannedPeerTracker");
            while (_running)
            {
                InvocationContext context;
                lock (_banPeerRequestTx)
                {
                    while (_banPeerRequestTx.Count == 0 && _running)
                    {
                        Monitor.Wait(_banPeerRequestTx);
                    }
                    
                    if (!_running)
                        break;
                    
                    context = _banPeerRequestTx.Dequeue();
                }

                try
                {
                    ProcessBanRequest(context);
                }
                catch (Exception exc)
                {
                    Logger.LogTrace($"Got exception processing ban request: {exc}");
                }

            }
            Logger.LogTrace("BannedPeerTracker stopped");
        }

        private void ProcessBanRequest(InvocationContext context)
        {
            var tx = context.Receipt.Transaction;
            if (!tx.To.Equals(ContractRegisterer.GovernanceContract))
                return;

            var signature = ContractEncoder.MethodSignatureAsInt(tx.Invocation);
            var decoder = new ContractDecoder(tx.Invocation.ToArray());
            if (signature != ContractEncoder.MethodSignatureAsInt(GovernanceInterface.MethodBanPeerRequestFrom))
                return;
            
            var cycle = NetworkManagerBase.CycleNumber(context.Receipt.Block);
            var args = decoder.Decode(GovernanceInterface.MethodBanPeerRequestFrom);
            var penalties = args[0] as UInt256 ?? throw new Exception("Failed to get penalties");
            var peerToBan = args[1] as byte[] ?? throw new Exception("Failed to get public key of banned peer");
            var sender = args[2] as byte[] ?? throw new Exception("Failed to get sender of ban request");
            if (penalties.ToBigInteger() < PeerPenalty.MaxPenaltyTolerance)
            {
                Logger.LogWarning(
                    $"{sender.ToHex()} requested to ban peer {peerToBan.ToHex()} for {penalties.ToBigInteger()} penalties"
                );
                throw new Exception("Invalid ban request");
            }
            var senderECDSAPublicKey = sender.ToPublicKey();
            if (!_validators.Contains(senderECDSAPublicKey))
            {
                Logger.LogTrace($"Non validator node {sender.ToHex()} sent request to ban peer {peerToBan.ToHex()}, ignoring");
                return;
            }
            var votes = _repostiroy.AddVoteForBannedPeer(cycle, peerToBan, sender);
            _banPeerVotes[peerToBan.ToPublicKey()] = votes;
            if (votes > ThresholdForBan)
            {
                // ban peer as more ThresholdForBan validators voted for this peer
                _banManager.BanPeer(peerToBan);
            }
        }

        private void ClearVotes()
        {
            _repostiroy.RemoveVotingCycle(_cycle);
            _banPeerVotes.Clear();
        }

        private void OnPeerBanned(object? sender, (byte[] publicKey, ulong penalties) @event)
        {
            Task.Factory.StartNew(() =>
            {
                var (publicKey, penalties) = @event;
                var receipt = MakeBanRequestTransaction(penalties, publicKey);
                var added = AddTransactionToPool(receipt);
                if (!added)
                {
                    Logger.LogWarning($"Falied to send MakeBanRequestTransaction tx");
                }
            }, TaskCreationOptions.LongRunning);
        }

        private bool AddTransactionToPool(TransactionReceipt receipt)
        {
            var error = _transactionPool.Add(receipt);
            if (error != OperatingError.Ok)
            {
                Logger.LogTrace($"Failed to add transaction {receipt.Hash} to pool with error {error}");
                return false;
            }
            return true;
        }

        public TransactionReceipt MakeBanRequestTransaction(ulong penalties, byte[] publicKey)
        {
            Logger.LogTrace("MakeBanRequestTransaction");
            var tx = _transactionBuilder.InvokeTransactionWithGasPrice(
                _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                ContractRegisterer.GovernanceContract,
                Money.Zero,
                GovernanceInterface.MethodBanPeerRequestFrom,
                0,
                new BigInteger(penalties).ToUInt256(),
                publicKey,
                _privateWallet.EcdsaKeyPair.PublicKey.EncodeCompressed()
            );
            return _transactionSigner.Sign(tx, _privateWallet.EcdsaKeyPair, HardforkHeights.IsHardfork_9Active(_blockManager.GetHeight() + 1));
        }

        public void Stop()
        {
            if (!_running)
                return;
            _running = false;
            lock (_banPeerRequestTx)
                Monitor.PulseAll(_banPeerRequestTx);
            _banRequestWorker.Join();
        }

        public void Dispose()
        {
            Stop();
        }
    }
}