using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Phorkus.Consensus;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Core.Blockchain;
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
        private readonly KeyPair _keyPair;
        private long CurrentEra { get; set; }

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            IMessageDeliverer messageDeliverer,
            IValidatorManager validatorManager,
            IConfigManager configManager, IBlockProducer blockProducer)
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            if (config?.ValidatorsEcdsaPublicKeys is null) throw new InvalidOperationException();
            if (config.TpkePrivateKey is null || config.TpkePublicKey is null || config.TpkeVerificationKey is null)
                throw new InvalidOperationException();
            if (config.ThresholdSignaturePrivateKey is null || config.ThresholdSignaturePublicKeySet is null)
                throw new InvalidOperationException();
            if (config.EcdsaPrivateKey is null) throw new InvalidOperationException();
            _messageDeliverer = messageDeliverer;
            _validatorManager = validatorManager;
            _blockProducer = blockProducer;
            var tpkePrivateKey =
                PrivateKey.FromBytes(config.TpkePrivateKey.HexToBytes() ?? throw new InvalidOperationException());
            var maxFaulty = (config.ValidatorsEcdsaPublicKeys.Count - 1) / 3;
            _wallet =
                new Wallet(config.ValidatorsEcdsaPublicKeys.Count,
                        maxFaulty) // TODO: store public key info in blockchain to be able to change validators
                    {
                        TpkePrivateKey = tpkePrivateKey,
                        TpkePublicKey = PublicKey.FromBytes(config.TpkePublicKey.HexToBytes()),
                        TpkeVerificationKey = VerificationKey.FromBytes(config.TpkeVerificationKey.HexToBytes()),
                        ThresholdSignaturePrivateKeyShare =
                            PrivateKeyShare.FromBytes(config.ThresholdSignaturePrivateKey.HexToBytes()),
                        ThresholdSignaturePublicKeySet = new PublicKeySet(
                            config.ThresholdSignaturePublicKeySet.Select(s => PublicKeyShare.FromBytes(s.HexToBytes())),
                            maxFaulty)
                    };
            _terminated = false;
            _keyPair = new KeyPair(config.EcdsaPrivateKey.HexToBytes().ToPrivateKey(), _crypto);
            _logger.LogDebug(
                $"Starting consensus as validator {_validatorManager.GetValidatorIndex(_keyPair.PublicKey)}");
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
                Thread.Sleep(5000);
                var broadcaster = EnsureEra(CurrentEra) ?? throw new InvalidOperationException();
                var rootId = new RootProtocolId(CurrentEra);
                var rootProtocol = new RootProtocol(_wallet, rootId, broadcaster, _blockProducer, _keyPair.PublicKey);
                broadcaster.RegisterProtocols(new[] {rootProtocol});
                broadcaster.InternalRequest(new ProtocolRequest<RootProtocolId, object?>(rootId, rootId, null));
                rootProtocol.WaitFinish();
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