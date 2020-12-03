using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.RootProtocol;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Config;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private static readonly ILogger<ConsensusManager> Logger = LoggerFactory.GetLoggerForClass<ConsensusManager>();
        private readonly IConsensusMessageDeliverer _consensusMessageDeliverer;
        private readonly IValidatorManager _validatorManager;
        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private readonly INetworkManager _networkManager;
        private readonly IBlockProducer _blockProducer;
        private readonly IBlockManager _blockManager;
        private bool _terminated;
        private readonly IPrivateWallet _privateWallet;
        private readonly IKeyGenManager _keyGenManager;

        private readonly IDictionary<long, List<(ConsensusMessage message, ECDSAPublicKey from)>> _postponedMessages
            = new Dictionary<long, List<(ConsensusMessage message, ECDSAPublicKey from)>>();

        private long CurrentEra { get; set; } = -1;

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();
        private readonly object _blockPersistedLock = new object();

        private readonly ulong _targetBlockInterval;

        public ConsensusManager(
            IConsensusMessageDeliverer consensusMessageDeliverer,
            IValidatorManager validatorManager,
            IBlockProducer blockProducer,
            IBlockManager blockManager,
            IPrivateWallet privateWallet,
            IValidatorAttendanceRepository validatorAttendanceRepository,
            IConfigManager configManager,
            INetworkManager networkManager,
            IKeyGenManager keyGenManager
        )
        {
            _consensusMessageDeliverer = consensusMessageDeliverer;
            _validatorManager = validatorManager;
            _blockProducer = blockProducer;
            _blockManager = blockManager;
            _privateWallet = privateWallet;
            _validatorAttendanceRepository = validatorAttendanceRepository;
            _networkManager = networkManager;
            _keyGenManager = keyGenManager;
            _terminated = false;

            _blockManager.OnBlockPersisted += BlockManagerOnOnBlockPersisted;
            _targetBlockInterval = configManager.GetConfig<BlockchainConfig>("blockchain")?.TargetBlockTime ?? 5_000;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            Logger.LogTrace($"Block {e.Header.Index} is persisted, notifying consensus which is on era {CurrentEra}");
            lock (_blockPersistedLock)
            {
                Monitor.PulseAll(_blockPersistedLock);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AdvanceEra(long newEra)
        {
            if (newEra < CurrentEra)
            {
                throw new InvalidOperationException($"Cannot advance backwards from era {CurrentEra} to era {newEra}");
            }

            Logger.LogTrace($"Advancing era from {CurrentEra} to {newEra}");

            for (var i = CurrentEra; i < newEra; ++i)
            {
                if (!IsValidatorForEra(i)) continue;
                var broadcaster = EnsureEra(i);
                Logger.LogTrace($"Terminating era {i}");
                broadcaster?.Terminate();
                _eras.Remove(i);
                lock (_postponedMessages)
                {
                    _postponedMessages.Remove(i);
                }
            }

            CurrentEra = newEra;
            _networkManager.AdvanceEra(CurrentEra);
        }

        private bool IsValidatorForEra(long era)
        {
            if (era <= 0) return false;
            return _validatorManager.IsValidatorForBlock(_privateWallet.EcdsaKeyPair.PublicKey, era);
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispatch(ConsensusMessage message, ECDSAPublicKey from)
        {
            var era = message.Validator.Era;
            if (era < CurrentEra)
                Logger.LogTrace($"Skipped message for era {era} since we already advanced to {CurrentEra}");
            else if (era > CurrentEra)
            {
                lock (_postponedMessages)
                {
                    _postponedMessages
                        .PutIfAbsent(era, new List<(ConsensusMessage message, ECDSAPublicKey from)>())
                        .Add((message, from));
                }
            }
            else
            {
                if (!IsValidatorForEra(era))
                {
                    Logger.LogWarning($"Skipped message for era {era} since we are not validator for this era");
                    return;
                }

                var fromIndex = _validatorManager.GetValidatorIndex(from, era - 1);
                EnsureEra(era)?.Dispatch(message, fromIndex);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Start(long startingEra)
        {
            CurrentEra = startingEra;
            _networkManager.AdvanceEra(CurrentEra);
            new Thread(Run).Start();
        }

        private void Run()
        {
            try
            {
                ulong lastBlock = 0;
                ulong prevBlock = 0;
                long delta = 0;
                for (;; CurrentEra += 1)
                {
                    _networkManager.AdvanceEra(CurrentEra);
                    Logger.LogTrace($"Advanced to era {CurrentEra}");
                    var now = TimeUtils.CurrentTimeMillis();
                    if (prevBlock > 0)
                        delta += (long) (lastBlock - prevBlock) - (long) _targetBlockInterval;
                    var waitTime = Math.Max(0, (long) _targetBlockInterval - delta);
                    prevBlock = lastBlock;
                    if (lastBlock + (ulong) waitTime > now)
                    {
                        Logger.LogTrace(
                            $"Waiting {lastBlock + (ulong) waitTime - now}ms until launching Root protocol"
                        );
                        Thread.Sleep(TimeSpan.FromMilliseconds(lastBlock + (ulong) waitTime - now));
                    }

                    for (var blockHeight = (long) _blockManager.GetHeight();
                        blockHeight != CurrentEra - 1;
                        blockHeight = (long) _blockManager.GetHeight()
                    )
                    {
                        Logger.LogTrace($"Block height is {blockHeight}, CurrentEra is {CurrentEra}");
                        if (blockHeight >= CurrentEra)
                        {
                            AdvanceEra(blockHeight + 1);
                            continue;
                        }

                        lock (_blockPersistedLock)
                        {
                            Monitor.Wait(_blockPersistedLock);
                        }
                    }

                    var weAreValidator = _validatorManager
                        .GetValidators(CurrentEra - 1)
                        .EcdsaPublicKeySet
                        .Contains(_privateWallet.EcdsaKeyPair.PublicKey);

                    var haveKeys = _privateWallet.HasKeyForKeySet(
                        _validatorManager.GetValidators(CurrentEra - 1).ThresholdSignaturePublicKeySet,
                        (ulong) CurrentEra
                    );

                    if (weAreValidator && !haveKeys)
                    {
                        Logger.LogError(
                            $"No required keys for block {CurrentEra}, need to rescan latest cycle to generate keys");
                        if (!_keyGenManager.RescanBlockChainForKeys(_validatorManager.GetValidators(CurrentEra - 1)))
                        {
                            Logger.LogError(
                                $"Failed to find relevant keygen in blockchain, cannot restore my key, waiting...");
                        }
                        else
                        {
                            Logger.LogWarning(
                                "Keys were not present but were found. State of protocols might be inconsistent now. Killing node to reboot it!");
                            Environment.Exit(0);
                            // this is unreachable but for the sake of logic
                            haveKeys = true;
                        }
                    }

                    if (!weAreValidator || !haveKeys)
                    {
                        Logger.LogWarning($"We are not validator for era {CurrentEra} (or keys are missing), waiting");
                        while ((long) _blockManager.GetHeight() < CurrentEra)
                        {
                            lock (_blockPersistedLock)
                            {
                                Monitor.Wait(_blockPersistedLock, TimeSpan.FromMilliseconds(1_000));
                            }
                        }
                    }
                    else
                    {
                        Logger.LogTrace("Starting new era and requesting root protocol");
                        var broadcaster = EnsureEra(CurrentEra) ?? throw new InvalidOperationException();
                        var rootId = new RootProtocolId(CurrentEra);
                        broadcaster.InternalRequest(
                            new ProtocolRequest<RootProtocolId, IBlockProducer>(rootId, rootId, _blockProducer)
                        );

                        lock (_postponedMessages)
                        {
                            if (_postponedMessages.TryGetValue(CurrentEra, out var savedMessages))
                            {
                                Logger.LogDebug(
                                    $"Processing {savedMessages.Count} postponed messages for era {CurrentEra}");
                                foreach (var (message, from) in savedMessages)
                                {
                                    var fromIndex = _validatorManager.GetValidatorIndex(from, CurrentEra - 1);
                                    Logger.LogTrace($"Handling postponed message: {message.PrettyTypeString()}");
                                    broadcaster.Dispatch(message, fromIndex);
                                }

                                _postponedMessages.Remove(CurrentEra);
                            }
                        }

                        while (!broadcaster.WaitFinish(TimeSpan.FromMilliseconds(1_000)))
                        {
                            if ((long) _blockManager.GetHeight() >= CurrentEra)
                            {
                                Logger.LogTrace("Aborting root protocol since block is already persisted");
                                break;
                            }

                            Logger.LogTrace("Still waiting for root protocol to terminate...");
                        }

                        broadcaster.Terminate();
                        _eras.Remove(CurrentEra);
                        Logger.LogTrace("Root protocol finished, waiting for new era");
                        lastBlock = TimeUtils.CurrentTimeMillis();
                    }

                    DefaultCrypto.ResetBenchmark();
                }
            }
            catch (Exception e)
            {
                Logger.LogCritical($"Fatal error in consensus, exiting: {e}");
                Environment.Exit(1);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Terminate()
        {
            _terminated = true;
        }

        /**
         * Initialize consensus broadcaster for era if necessary. May throw if era is too far in the past or future
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private EraBroadcaster? EnsureEra(long era)
        {
            if (era <= 0) return null;
            if (_eras.ContainsKey(era)) return _eras[era];
            if (_terminated)
            {
                Logger.LogWarning($"Broadcaster for era {era} not created since consensus is terminated");
                return null;
            }

            return _eras[era] = new EraBroadcaster(era, _consensusMessageDeliverer, _validatorManager, _privateWallet,
                _validatorAttendanceRepository);
        }
    }
}