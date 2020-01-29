using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        private readonly ICrypto _crypto;
        private bool _terminated;
        private readonly IWallet _wallet;
        private readonly KeyPair _keyPair;
        private long CurrentEra { get; set; }

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            IMessageDeliverer messageDeliverer,
            IValidatorManager validatorManager,
            IConfigManager configManager,
            ICrypto crypto
        )
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            _messageDeliverer = messageDeliverer;
            _validatorManager = validatorManager;
            _crypto = crypto;
            var tpkePrivateKey = PrivateKey.FromBytes(config.TpkePrivateKey.HexToBytes());
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
            _keyPair = new KeyPair(config.EcdsaPrivateKey.HexToBytes().ToPrivateKey(), crypto);
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
            EnsureEra(era).Dispatch(message);
        }

        public void Start(long startingEra)
        {
            CurrentEra = startingEra;
            var broadcaster = EnsureEra(CurrentEra);
            var rootId = new HoneyBadgerId(CurrentEra);
            var rootProtocol = new HoneyBadger(rootId, _wallet, broadcaster);
            broadcaster.RegisterProtocols(new[] {rootProtocol});
            broadcaster.InternalRequest(
                new ProtocolRequest<HoneyBadgerId, IRawShare>(null, rootId, new RawShare(new byte[] { }, 0))
            );
        }

        public void Terminate()
        {
            _terminated = true;
        }

        /**
         * Initialize consensus broadcaster for era if necessary. May throw if era is too far in the past or future
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private EraBroadcaster EnsureEra(long era)
        {
            if (_eras.ContainsKey(era)) return _eras[era];
            _logger.LogDebug($"Creating broadcaster for era {era}");
            if (_terminated)
            {
                _logger.LogWarning($"Broadcaster for era {era} not created since consensus is terminated");
                return null;
            }

            _logger.LogDebug($"Created broadcaster for era {era}");
            return _eras[era] = new EraBroadcaster(era, _messageDeliverer, _validatorManager, _keyPair, _wallet,
                _crypto);
        }
    }
}