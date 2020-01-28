using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Consensus;
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
        private readonly ILogger<IConsensusBroadcaster> _logger;
        private readonly INetworkManager _networkManager;
        private readonly IValidatorManager _validatorManager;
        private bool _terminated;
        private readonly IWallet _wallet;
        private readonly KeyPair _keyPair;
        private long CurrentEra { get; set; }

        private readonly Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            INetworkManager networkManager,
            IValidatorManager validatorManager,
            IConfigManager configManager,
            ICrypto crypto,
            ILogger<IConsensusBroadcaster> logger
        )
        {
            var config = configManager.GetConfig<ConsensusConfig>("consensus");
            _networkManager = networkManager;
            _validatorManager = validatorManager;
            var tpkePrivateKey = PrivateKey.FromBytes(config.TpkePrivateKey.HexToBytes());
            var maxFaulty = (config.ValidatorsEcdsaPublicKeys.Count - 1) / 3;
            _wallet = new Wallet(config.ValidatorsEcdsaPublicKeys.Count, maxFaulty)
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
            _logger = logger;
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
            InitializeEra(era);
            _eras[era].Dispatch(message);
        }

        public void Start(long startingEra)
        {
            CurrentEra = startingEra;
        }

        public void Terminate()
        {
            _terminated = true;
        }

        /**
         * Initialize consensus broadcaster for era if necessary. May throw if era is too far in the past or future
         */
        [MethodImpl(MethodImplOptions.Synchronized)]
        private void InitializeEra(long era)
        {
            if (_eras.ContainsKey(era)) return;
            _logger.LogDebug($"Creating broadcaster for era {era}");
            if (_terminated)
            {
                _logger.LogWarning($"Broadcaster for era {era} not created since consensus is terminated");
                return;
            }

            _eras[era] = new EraBroadcaster(era, _networkManager, _validatorManager, _keyPair, _wallet, _logger);
            _logger.LogDebug($"Created broadcaster for era {era}");
        }
    }
}