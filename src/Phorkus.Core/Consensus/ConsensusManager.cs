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
        private readonly IWallet _wallet;
        private readonly ECDSAKeyPair _keyPair;
        private long CurrentEra { get; set; }

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
            var validatorsKeys =
                config.ValidatorsEcdsaPublicKeys?.Select(key => key.HexToBytes().ToPublicKey()).ToArray() ??
                throw new ArgumentNullException();
            var maxFaulty = (validatorsKeys.Length - 1) / 3;

            var tpkePrivateKey =
                PrivateKey.FromBytes(config.TpkePrivateKey?.HexToBytes() ?? throw new ArgumentNullException());
            var tpkePublicKey =
                PublicKey.FromBytes(config.TpkePublicKey?.HexToBytes() ?? throw new ArgumentNullException());
            var tpkeVerificationKey =
                VerificationKey.FromBytes(config.TpkeVerificationKey?.HexToBytes() ??
                                          throw new ArgumentNullException());
            var thresholdSignaturePublicKeySet = new PublicKeySet(
                config.ThresholdSignaturePublicKeySet?
                    .Select(s => PublicKeyShare.FromBytes(s.HexToBytes())) ?? throw new ArgumentNullException(),
                maxFaulty
            );
            var thresholdSignaturePrivateKeyShare = PrivateKeyShare.FromBytes(
                config.ThresholdSignaturePrivateKey?.HexToBytes() ??
                throw new ArgumentNullException()
            );
            _keyPair = new ECDSAKeyPair(
                config.EcdsaPrivateKey?.HexToBytes().ToPrivateKey() ?? throw new ArgumentNullException(), _crypto
            );
            if (!validatorManager.Validators.SequenceEqual(validatorsKeys))
            {
                throw new InvalidOperationException("Inconsistent validator numeration");
            }

            if (thresholdSignaturePublicKeySet.GetIndex(thresholdSignaturePrivateKeyShare.GetPublicKeyShare()) !=
                validatorManager.GetValidatorIndex(_keyPair.PublicKey))
            {
                throw new InvalidOperationException("Inconsistent validator numeration");
            }

            _messageDeliverer = messageDeliverer;
            _validatorManager = validatorManager;
            _blockProducer = blockProducer;
            _wallet = new Wallet(
                config.ValidatorsEcdsaPublicKeys.Count, maxFaulty,
                tpkePublicKey, tpkePrivateKey, tpkeVerificationKey,
                thresholdSignaturePublicKeySet, thresholdSignaturePrivateKeyShare,
                _keyPair.PublicKey, _keyPair.PrivateKey, validatorsKeys
            ); // TODO: store public key info in blockchain to be able to change validators
            _terminated = false;
            blockManager.OnBlockPersisted += BlockManagerOnOnBlockPersisted;
            _logger.LogDebug(
                $"Starting consensus as validator {_validatorManager.GetValidatorIndex(_keyPair.PublicKey)}");
        }

        private void BlockManagerOnOnBlockPersisted(object sender, Block e)
        {
            _logger.LogDebug($"Block {e.Header.Index} is persisted, terminating corresponding era");
            EnsureEra((long) e.Header.Index)?.Terminate();
        }

        public void AdvanceEra(long newEra)
        {
            if (newEra < CurrentEra)
            {
                throw new InvalidOperationException($"Cannot advance backwards from era {CurrentEra} to era {newEra}");
            }

            CurrentEra = newEra;
        }


        public void Dispatch(ConsensusMessage message)
        {
            var era = message.Validator.Era;
            if (era < CurrentEra)
            {
                _logger.LogDebug($"Skipped message for era {era} since we already advanced to {CurrentEra}");
                return;
            }

            EnsureEra(era)?.Dispatch(message);
        }

        public void Start(long startingEra)
        {
            CurrentEra = startingEra;
            new Thread(Run).Start();
        }

        private void Run()
        {
            for (;; CurrentEra += 1)
            {
                Thread.Sleep(30_000);
                var broadcaster = EnsureEra(CurrentEra) ?? throw new InvalidOperationException();
                var rootId = new RootProtocolId(CurrentEra);
                broadcaster.InternalRequest(
                    new ProtocolRequest<RootProtocolId, IBlockProducer>(rootId, rootId, _blockProducer)
                );
                broadcaster.WaitFinish();
                broadcaster.Terminate();
                _eras.Remove(CurrentEra);
                _logger.LogDebug("Root protocol finished, waiting for new era...");
            }
        }

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
            return _eras[era] = new EraBroadcaster(era, _messageDeliverer, _validatorManager, _keyPair, _wallet);
        }
    }
}