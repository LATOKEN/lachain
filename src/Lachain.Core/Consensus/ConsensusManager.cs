using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.RootProtocol;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Config;
using Lachain.Core.Network;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    /*
        This is the main class responsible for starting and co-ordinating consensus for different
        eras. 
    */
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
        private readonly object _erasLock = new object();
        private readonly object _blockPersistedLock = new object();
        private ulong _lastSignedHeaderReceived = 0;

        private readonly ulong _targetBlockInterval;

        public ConsensusManager(
            IConsensusMessageDeliverer consensusMessageDeliverer,
            IValidatorManager validatorManager,
            IBlockProducer blockProducer,
            IBlockManager blockManager,
            IBlockSynchronizer blockSynchronizer,
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
            blockSynchronizer.OnSignedBlockReceived += BlockSynchronizerOnOnSignedBlockReceived;
            _targetBlockInterval = configManager.GetConfig<BlockchainConfig>("blockchain")?.TargetBlockTime ?? 5_000;
        }

        private void BlockSynchronizerOnOnSignedBlockReceived(object sender, ulong block)
        {
            _lastSignedHeaderReceived = block;
        }

        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            Logger.LogTrace($"Block {e.Header.Index} is persisted, notifying consensus which is on era {CurrentEra}");
            lock (_blockPersistedLock)
            {
                Monitor.PulseAll(_blockPersistedLock);
            }
        }

        private bool IsValidatorForEra(long era)
        {
            if (era <= 0) return false;
            return _validatorManager.IsValidatorForBlock(_privateWallet.EcdsaKeyPair.PublicKey, era);
        }


        public void Dispatch(ConsensusMessage message, ECDSAPublicKey from)
        {
            lock (_erasLock)
            {
                var era = message.Validator.Era;
                if (era < CurrentEra)
                {
                    Logger.LogTrace($"Skipped message for era {era} since we already advanced to {CurrentEra}");
                    return;
                }

                if (era == CurrentEra && _eras[era].Ready)
                {
                    var broadcaster = _eras[era];
                    if (broadcaster.GetMyId() == -1)
                    {
                        Logger.LogWarning($"Skipped message for era {era} since we are not validator for this era");
                        return;
                    }

                    var fromIndex = broadcaster.GetIdByPublicKey(from);
                    if (fromIndex == -1)
                    {
                        Logger.LogWarning(
                            $"Skipped message for era {era} since it came from {from.ToHex()} who is not validator for this era");
                        return;
                    }

                    broadcaster.Dispatch(message, fromIndex);
                }
                else
                {
                    lock (_postponedMessages)
                    {
                        // This queue can get very long and may cause node shut down due to insufficient memory
                        _postponedMessages
                            .PutIfAbsent(era, new List<(ConsensusMessage message, ECDSAPublicKey from)>())
                            .Add((message, from));
                    }
                }
            }
        }

        public void Start(ulong startingEra)
        {
            _networkManager.AdvanceEra(startingEra);
            new Thread(() => Run(startingEra)).Start();
        }

        private void FinishEra()
        {
            lock (_erasLock)
            {
                var broadcaster = _eras[CurrentEra];
                lock (broadcaster)
                {
                    DefaultCrypto.ResetBenchmark();
                    broadcaster.Terminate();
                    _eras.Remove(CurrentEra);
                    
                    CurrentEra += 1;
                    _eras[CurrentEra] = new EraBroadcaster(
                        CurrentEra, _consensusMessageDeliverer, _privateWallet, _validatorAttendanceRepository
                    );
                    Logger.LogTrace($"Current Era is advanced. Current Era: {CurrentEra}");
                }
            }
        }

        private void Run(ulong startingEra)
        {
            Logger.LogTrace($"Starting, startingEra {startingEra}");
            CurrentEra = (long) startingEra;
            lock (_erasLock)
            {
                Logger.LogTrace("Create EraBroadcaster");
                _eras[CurrentEra] = new EraBroadcaster(
                    CurrentEra, _consensusMessageDeliverer, _privateWallet, _validatorAttendanceRepository
                );
            }

            try
            {
                ulong lastEra = 0, lastEraStartTime = 0;
                for (; !_terminated; FinishEra())
                {
                    Logger.LogDebug(
                        $"Start processing era {CurrentEra}, waiting for block {CurrentEra - 1} to be persisted");
                    var height = (long) _blockManager.GetHeight();
                    IPublicConsensusKeySet? validators;
                    for (;; height = (long) _blockManager.GetHeight())
                    {
                        validators = _validatorManager.GetValidators(CurrentEra - 1);
                        if (height >= CurrentEra - 1 && validators != null) break;
                        Logger.LogTrace(
                            $"Block height is {height}, CurrentEra is {CurrentEra}, validators: {(validators == null ? "NO" : "OK")}, waiting");
                        lock (_blockPersistedLock) Monitor.Wait(_blockPersistedLock);
                    }

                    Logger.LogTrace(
                        $"Block {CurrentEra - 1} persisted, {validators.N} validators: [{string.Join(", ", validators.EcdsaPublicKeySet.Select(k => k.ToHex()))}]"
                    );

                    EraBroadcaster? broadcaster;
                    lock (_erasLock)
                    {
                        broadcaster = _eras[CurrentEra];
                        broadcaster.SetValidatorKeySet(validators);
                    }

                    bool weAreValidator;
                    lock (broadcaster)
                    {
                        if (height >= CurrentEra)
                        {
                            Logger.LogTrace(
                                $"Block {CurrentEra} is already present, block height is {height}, won't start protocols for this era");
                            continue;
                        }

                        weAreValidator =
                            validators.EcdsaPublicKeySet.Contains(_privateWallet.EcdsaKeyPair.PublicKey);

                        var haveKeys = _privateWallet.HasKeyForKeySet(
                            validators.ThresholdSignaturePublicKeySet,
                            (ulong) CurrentEra
                        );

                        if (weAreValidator && !haveKeys)
                        {
                            Logger.LogError(
                                $"No required keys for block {CurrentEra}, need to rescan previous cycle to generate keys");
                            if (!_keyGenManager.RescanBlockChainForKeys(validators!))
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

                            if (!weAreValidator || !haveKeys)
                            {
                                var reason = haveKeys ? "(keys are missing)" : "";
                                Logger.LogWarning(
                                    $"We are not validator for era {CurrentEra} {reason}, waiting for block {CurrentEra}");
                                while ((long) _blockManager.GetHeight() < CurrentEra)
                                {
                                    lock (_blockPersistedLock)
                                    {
                                        Monitor.Wait(_blockPersistedLock, TimeSpan.FromMilliseconds(1_000));
                                    }
                                }

                                continue;
                            }
                        }

                        if (weAreValidator)
                        {
                            var now = TimeUtils.CurrentTimeMillis();
                            Logger.LogTrace(
                                $"Starting new era {CurrentEra}, last era in consensus {lastEra}, took {now - lastEraStartTime}ms");
                            if (lastEra == (ulong) CurrentEra - 1 && now - lastEraStartTime < _targetBlockInterval)
                            {
                                var delta = _targetBlockInterval - (now - lastEraStartTime);
                                Logger.LogTrace($"Waiting {delta}ms to start root protocol");
                                Thread.Sleep(TimeSpan.FromMilliseconds(delta));
                            }

                            lastEra = (ulong) CurrentEra;
                            lastEraStartTime = TimeUtils.CurrentTimeMillis();
                            Logger.LogTrace($"Requesting root protocol for era {CurrentEra}");
                            var rootId = new RootProtocolId(CurrentEra);
                            broadcaster.InternalRequest(
                                new ProtocolRequest<RootProtocolId, IBlockProducer>(rootId, rootId,
                                    _blockProducer)
                            );

                            lock (_postponedMessages)
                            {
                                if (_postponedMessages.TryGetValue(CurrentEra, out var savedMessages))
                                {
                                    Logger.LogDebug(
                                        $"Processing {savedMessages.Count} postponed messages for era {CurrentEra}");
                                    foreach (var (message, from) in savedMessages)
                                    {
                                        var fromIndex = validators.GetValidatorIndex(from);
                                        // If a validator from some previous era sends message for some future era, the 
                                        // message will come here, but it could be that the validator is not a validator
                                        // anymore, in that case the fromIndex will be -1 and may halt some process.
                                        // So we need to check this and discard such messages
                                        if (fromIndex == -1)
                                        {
                                            Logger.LogWarning(
                                                $"Skipped message for era {CurrentEra} since it came from "
                                                + $"{from.ToHex()} who is not validator for this era");
                                            continue;
                                        }
                                        Logger.LogTrace(
                                            $"Handling postponed message: {message.PrettyTypeString()}");
                                        broadcaster.Dispatch(message, fromIndex);
                                    }

                                    _postponedMessages.Remove(CurrentEra);
                                }
                            }
                        }
                    }

                    if (weAreValidator)
                    {
                        while (!broadcaster.WaitFinish(TimeSpan.FromMilliseconds(1_000)))
                        {
                            if ((long) _lastSignedHeaderReceived >= CurrentEra ||
                                (long) _blockManager.GetHeight() >= CurrentEra)
                            {
                                Logger.LogTrace("Aborting root protocol since block is already persisted");
                                break;
                            }

                            Logger.LogTrace($"Still waiting for root protocol (E={CurrentEra}) to terminate...");
                        }
                    }
                    
                    Logger.LogTrace("Root protocol finished, waiting for new era");
                }
            }
            catch (Exception e)
            {
                Logger.LogCritical($"Fatal error in consensus, exiting: {e}");
                Environment.Exit(1);
            }
        }

        public void Terminate()
        {
            _terminated = true;
        }

        public EraBroadcaster? GetEraBroadcaster()
        {
            lock (_erasLock)
            {
                if (_eras.ContainsKey(CurrentEra))
                {
                    return _eras[CurrentEra];
                }
                else
                {
                    return null;
                }
            }
        }
    }
}