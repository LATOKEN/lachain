using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Phorkus.Consensus;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.RootProtocol;
using Phorkus.Consensus.TPKE;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Blockchain.Validators;
using Phorkus.Core.Config;
using Phorkus.Crypto;
using Phorkus.Crypto.ThresholdSignature;
using Phorkus.Crypto.TPKE;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using PublicKey = Phorkus.Crypto.TPKE.PublicKey;

namespace Phorkus.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private readonly ILogger<ConsensusManager> _logger = LoggerFactory.GetLoggerForClass<ConsensusManager>();
        private readonly IMessageDeliverer _messageDeliverer;
        private readonly IValidatorManager _validatorManager;
        private readonly IBlockProducer _blockProducer;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private bool _terminated;
        private readonly IPrivateConsensusKeySet _consensusKeySet;
        private long CurrentEra { get; set; } = -1;

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            IMessageDeliverer messageDeliverer,
            IValidatorManager validatorManager,
            IConfigManager configManager,
            IBlockProducer blockProducer,
            IBlockManager blockManager
        )
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config is null) throw new ArgumentNullException(nameof(config));

            var tpkePrivateKey =
                PrivateKey.FromBytes(config.TpkePrivateKey?.HexToBytes() ?? throw new ArgumentNullException());
            var thresholdSignaturePrivateKeyShare = PrivateKeyShare.FromBytes(
                config.ThresholdSignaturePrivateKey?.HexToBytes() ??
                throw new ArgumentNullException()
            );
            var keyPair = new ECDSAKeyPair(
                config.EcdsaPrivateKey?.HexToBytes().ToPrivateKey() ?? throw new ArgumentNullException(), _crypto
            );

            _messageDeliverer = messageDeliverer;
            _validatorManager = validatorManager;
            _blockProducer = blockProducer;
            _terminated = false;

            _consensusKeySet = new PrivateConsensusKeySet(keyPair, tpkePrivateKey, thresholdSignaturePrivateKeyShare);
            blockManager.OnBlockPersisted += BlockManagerOnOnBlockPersisted;
        }

        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            _logger.LogDebug($"Block {e.Header.Index} is persisted, terminating corresponding era");
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
                var broadcaster = EnsureEra(i);
                broadcaster.Terminate();
                _eras.Remove(i);
            }

            CurrentEra = newEra;
        }


        [MethodImpl(MethodImplOptions.Synchronized)]
        public void Dispatch(ConsensusMessage message, int from)
        {
            var era = message.Validator.Era;
            if (CurrentEra == -1)
            {
                _logger.LogWarning($"Consensus has not been started yet, skipping message with era {era}");
            }

            if (era < CurrentEra)
            {
                _logger.LogDebug($"Skipped message for era {era} since we already advanced to {CurrentEra}");
                return;
            }

            EnsureEra(era)?.Dispatch(message, from);
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
                const ulong minBlockInterval = 5_000;
                for (;; CurrentEra += 1)
                {
                    var now = TimeUtils.CurrentTimeMillis();
                    if (lastBlock + minBlockInterval > now)
                    {
                        Thread.Sleep(TimeSpan.FromMilliseconds(lastBlock + minBlockInterval - now));
                    }

                    var broadcaster = EnsureEra(CurrentEra) ?? throw new InvalidOperationException();
                    var rootId = new RootProtocolId(CurrentEra);
                    broadcaster.InternalRequest(
                        new ProtocolRequest<RootProtocolId, IBlockProducer>(rootId, rootId, _blockProducer)
                    );
                    broadcaster.WaitFinish();
                    broadcaster.Terminate();
                    _eras.Remove(CurrentEra);
                    _logger.LogDebug("Root protocol finished, waiting for new era...");
                    lastBlock = TimeUtils.CurrentTimeMillis();
                }
            }
            catch (Exception e)
            {
                _logger.LogCritical($"Fatal error in consensus, exiting: {e}");
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
            if (_eras.ContainsKey(era)) return _eras[era];
            _logger.LogDebug($"Creating broadcaster for era {era}");
            if (_terminated)
            {
                _logger.LogWarning($"Broadcaster for era {era} not created since consensus is terminated");
                return null;
            }

            _logger.LogDebug($"Created broadcaster for era {era}");
            return _eras[era] = new EraBroadcaster(era, _messageDeliverer, _validatorManager, _consensusKeySet);
        }
    }
}