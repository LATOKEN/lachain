using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Consensus.Messages;
using Lachain.Consensus.RootProtocol;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Blockchain.ContractManager.Standards;
using Lachain.Core.Blockchain.Interface;
using Lachain.Core.Blockchain.OperationManager;
using Lachain.Core.Blockchain.Pool;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.State;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private static readonly ILogger<ConsensusManager> Logger = LoggerFactory.GetLoggerForClass<ConsensusManager>();
        private readonly IMessageDeliverer _messageDeliverer;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockProducer _blockProducer;
        private readonly IBlockchainContext _blockchainContext;
        private bool _terminated;
        private readonly IPrivateWallet _privateWallet;
        private readonly ITransactionPool _transactionPool;
        private readonly ITransactionBuilder _transactionBuilder;
        private readonly ITransactionSigner _transactionSigner;

        private readonly IDictionary<long, List<(ConsensusMessage message, ECDSAPublicKey from)>> _postponedMessages
            = new Dictionary<long, List<(ConsensusMessage message, ECDSAPublicKey from)>>();

        private long CurrentEra { get; set; } = -1;

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            IMessageDeliverer messageDeliverer,
            IValidatorManager validatorManager,
            IBlockProducer blockProducer,
            IBlockManager blockManager,
            IBlockchainContext blockchainContext,
            IPrivateWallet privateWallet,
            ITransactionPool transactionPool,
            ITransactionBuilder transactionBuilder,
            ITransactionSigner transactionSigner
        )
        {
            _messageDeliverer = messageDeliverer;
            _validatorManager = validatorManager;
            _blockProducer = blockProducer;
            _blockchainContext = blockchainContext;
            _privateWallet = privateWallet;
            _transactionPool = transactionPool;
            _transactionBuilder = transactionBuilder;
            _transactionSigner = transactionSigner;
            _terminated = false;

            blockManager.OnBlockPersisted += BlockManagerOnOnBlockPersisted;
        }

        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            Logger.LogDebug($"Block {e.Header.Index} is persisted, terminating corresponding era");
            if ((long) e.Header.Index >= CurrentEra)
            {
                AdvanceEra((long) e.Header.Index);
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AdvanceEra(long newEra)
        {
            if (newEra < CurrentEra)
            {
                throw new InvalidOperationException($"Cannot advance backwards from era {CurrentEra} to era {newEra}");
            }

            for (var i = CurrentEra; i <= newEra; ++i)
            {
                if (!IsValidatorForEra(i)) continue;
                var broadcaster = EnsureEra(i);
                broadcaster?.Terminate();
                _eras.Remove(i);
            }

            CurrentEra = newEra;
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
            if (CurrentEra == -1)
                Logger.LogWarning($"Consensus has not been started yet, skipping message with era {era}");
            else if (era < CurrentEra)
                Logger.LogDebug($"Skipped message for era {era} since we already advanced to {CurrentEra}");
            else if (era > CurrentEra)
                _postponedMessages
                    .PutIfAbsent(era, new List<(ConsensusMessage message, ECDSAPublicKey from)>())
                    .Add((message, from));
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
            new Thread(Run).Start();
        }

        private void Run()
        {
            try
            {
                ulong lastBlock = 0;
                // const ulong minBlockInterval = 5_000;
                const ulong minBlockInterval = 1_000;
                for (;; CurrentEra += 1)
                {
                    if (CurrentEra == 10 && _privateWallet.EcdsaKeyPair.PublicKey.EncodeCompressed().ToHex() == "0x023aa2e28f6f02e26c1f6fcbcf80a0876e55a320cefe563a3a343689b3fd056746")
                    {
                        Logger.LogError("!!!!! CHANGE VALIDATORS !!!!!");
                        var txPool = _transactionPool;
                        var signer = _transactionSigner;
                        var builder = _transactionBuilder;
                        var newValidators = new[]
                        {
                            "023aa2e28f6f02e26c1f6fcbcf80a0876e55a320cefe563a3a343689b3fd056746".HexToBytes(),
                            "02867b79666bac79f845ed33f4b2b0a964f9ac5b9be676b82bd9a01edcdad42415".HexToBytes(),
                            "02a3ec3502ef652a969613696ec98a7bfd2c7b87d20efed47b3af726285d197d3c".HexToBytes(),
                            "0272f1805337a1d2a0ad98c1e823b8103113a1824c1d8899187eaae748dd78229d".HexToBytes()
                        }; 
                        var tx = builder.InvokeTransaction(
                            _privateWallet.EcdsaKeyPair.PublicKey.GetAddress(),
                            ContractRegisterer.GovernanceContract,
                            Money.Zero,
                            GovernanceInterface.MethodChangeValidators,
                            (object) newValidators
                        );
                        txPool.Add(signer.Sign(tx, _privateWallet.EcdsaKeyPair));                
                    }
                    
                    var now = TimeUtils.CurrentTimeMillis();
                    if (lastBlock + minBlockInterval > now)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(lastBlock + minBlockInterval - now));
                    }

                    if ((long) _blockchainContext.CurrentBlockHeight >= CurrentEra)
                    {
                        AdvanceEra((long) _blockchainContext.CurrentBlockHeight);
                        continue;
                    }

                    var weAreValidator = true;
                    try
                    {
                        if (!_validatorManager.GetValidators(CurrentEra - 1).EcdsaPublicKeySet
                            .Contains(_privateWallet.EcdsaKeyPair.PublicKey))
                            weAreValidator = false;
                    }
                    catch (ConsensusStateNotPresentException e)
                    {
                        Logger.LogError("Consensus state is not present (why?)");
                    }
                    if (!weAreValidator)
                    {
                        Logger.LogError($"We are not validator for era {CurrentEra}, waiting");
                        while ((long) _blockchainContext.CurrentBlockHeight != CurrentEra)
                        {
                            Thread.Sleep(TimeSpan.FromMilliseconds(1_000));
                        }
                    }
                    else
                    {
                        var publicKeySet = _validatorManager.GetValidators(CurrentEra - 1);
                        Logger.LogError($"!!!!!!!!!!!!! ERA: {CurrentEra}");
                        Logger.LogError($"!!!!!!!!!!!!! MY INDEX: {_validatorManager.GetValidatorIndex(_privateWallet.EcdsaKeyPair.PublicKey, CurrentEra - 1)}");
                        Logger.LogError($"!!!!!!!!!!!!! TS PUBLIC KEYS:");
                        foreach (var (key, i) in _validatorManager.GetValidators(CurrentEra - 1).ThresholdSignaturePublicKeySet.Keys.WithIndex())
                            Logger.LogError($"!!!!!!!!!!!!!     {i}: {key.ToBytes().ToHex()}");
                        Logger.LogError($"!!!!!!!!!!!!! My ts privkey: {_privateWallet.GetThresholdSignatureKeyForBlock((ulong) CurrentEra - 1).ToBytes().ToHex()}");
                        Logger.LogError($"!!!!!!!!!!!!! My ts pubkey: {_privateWallet.GetThresholdSignatureKeyForBlock((ulong) CurrentEra - 1).GetPublicKeyShare().ToBytes().ToHex()}");
                        var broadcaster = EnsureEra(CurrentEra) ?? throw new InvalidOperationException();
                        var rootId = new RootProtocolId(CurrentEra);
                        broadcaster.InternalRequest(
                            new ProtocolRequest<RootProtocolId, IBlockProducer>(rootId, rootId, _blockProducer)
                        );

                        if (_postponedMessages.TryGetValue(CurrentEra, out var savedMessages))
                        {
                            Logger.LogDebug($"Processing {savedMessages.Count} postponed messages for era {CurrentEra}");
                            foreach (var (message, from) in savedMessages)
                            {
                                var fromIndex = _validatorManager.GetValidatorIndex(from, CurrentEra - 1);
                                broadcaster.Dispatch(message, fromIndex);
                            }

                            _postponedMessages.Remove(CurrentEra);
                        }

                        broadcaster.WaitFinish();
                        broadcaster.Terminate();
                        _eras.Remove(CurrentEra);
                        Logger.LogDebug("Root protocol finished, waiting for new era...");
                        lastBlock = TimeUtils.CurrentTimeMillis();
                    }
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
            Logger.LogDebug($"Creating broadcaster for era {era}");
            if (_terminated)
            {
                Logger.LogWarning($"Broadcaster for era {era} not created since consensus is terminated");
                return null;
            }

            Logger.LogDebug($"Created broadcaster for era {era}");
            return _eras[era] = new EraBroadcaster(era, _messageDeliverer, _validatorManager, _privateWallet);
        }
    }
}