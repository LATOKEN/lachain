using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phorkus.Consensus;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;

namespace Phorkus.Core.Consensus
{
    public class ConsensusManager : IConsensusManager
    {
        private readonly ILogger<IConsensusBroadcaster> _logger;
        private readonly INetworkManager _networkManager;
        private readonly ECDSAPublicKey[] _validatorsKeys;
        private readonly int _myIndex;
        private bool _terminated;
        private readonly IWallet _wallet;
        public long CurrentEra { get; private set; }

        private Dictionary<long, EraBroadcaster> _eras = new Dictionary<long, EraBroadcaster>();

        public ConsensusManager(
            long startingEra, INetworkManager networkManager, ECDSAPublicKey[] validatorsKeys,
            int myIndex, IWallet wallet, ILogger<IConsensusBroadcaster> logger
        )
        {
            _networkManager = networkManager;
            _wallet = wallet;
            _validatorsKeys = validatorsKeys;
            _myIndex = myIndex;
            _logger = logger;
            _terminated = false;
            CurrentEra = startingEra;
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

            _eras[era] = new EraBroadcaster(era, _networkManager, _validatorsKeys, _myIndex, _wallet, _logger);
            _logger.LogDebug($"Created broadcaster for era {era}");
        }
    }
}