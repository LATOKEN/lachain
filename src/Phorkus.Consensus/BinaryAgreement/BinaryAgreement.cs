using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonCoin.ThresholdCrypto;
using Phorkus.Consensus.Messages;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Consensus.BinaryAgreement
{
    class BinaryAgreement : IBinaryAgreement
    {
        private readonly int _parties, _faulty;
        private readonly BinaryAgreementId _agreementId;
        private readonly PublicKeySet _publicKeySet;
        private readonly PrivateKeyShare _privateKeyShare;
        private readonly IConsensusBroadcaster _consensusBroadcaster;
        private readonly IMessageDispatcher _dispatcher;
        private bool _finished;
        private bool _proposed;
        private ulong _currentEpoch;
        private bool _estimate;
        private BoolSet _currentValues;

        public BinaryAgreement(
            int n, int f, BinaryAgreementId agreementId,
            PublicKeySet publicKeySet, PrivateKeyShare privateKeyShare,
            IConsensusBroadcaster consensusBroadcaster,
            IMessageDispatcher dispatcher
        )
        {
            _parties = n;
            _faulty = f;
            _agreementId = agreementId;
            _publicKeySet = publicKeySet;
            _privateKeyShare = privateKeyShare;
            _consensusBroadcaster = consensusBroadcaster;
            _dispatcher = dispatcher;
            _finished = false;
            _proposed = false;
            _currentEpoch = 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void ProposeValue(bool value)
        {
            if (_currentEpoch != 0 || _proposed)
                throw new InvalidOperationException("Cannot propose value: protocol is already running");
            if (_finished) return;
            _proposed = true;
            _estimate = value;
            StartEpoch();
        }

        public bool IsFinished()
        {
            return _finished;
        }

        public IProtocolIdentifier Id => _agreementId;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleMessage(ConsensusMessage message)
        {
            if (_finished) return;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void HandleInternalMessage(InternalMessage message)
        {
            if (_finished) return;
            switch (message)
            {
                case BroadcastCompleted broadcastCompleted:
                {
                    if (
                        broadcastCompleted.BroadcastId.Era != _agreementId.Era ||
                        broadcastCompleted.BroadcastId.Agreement != _agreementId.Agreement ||
                        broadcastCompleted.BroadcastId.Epoch != _currentEpoch
                    )
                        throw new InvalidOperationException("broadcast id mismatch");
                    _currentValues = broadcastCompleted.Values;
                    var coinId = new CoinId(_agreementId.Era, _agreementId.Agreement, _currentEpoch);
                    ICommonCoin coin = new CommonCoin.CommonCoin(_publicKeySet, _privateKeyShare, coinId,
                        _consensusBroadcaster);
                    _dispatcher.RegisterAlgorithm(coin, coinId);
                    coin.RequestCoin();
                    return;
                }
                case CoinTossed coinTossed:
                    if (
                        coinTossed.CoinId.Era != _agreementId.Era ||
                        coinTossed.CoinId.Agreement != _agreementId.Agreement ||
                        coinTossed.CoinId.Epoch != _currentEpoch
                    )
                        throw new InvalidOperationException("coin id mismatch");
                    _estimate = coinTossed.CoinValue;
                    if (_currentValues.Count() == 1)
                    {
                        _estimate = _currentValues.Values().First();
                        if (_estimate == coinTossed.CoinValue)
                        {
                            _finished = true;
                            _consensusBroadcaster.MessageSelf(new AgreementReached(_agreementId, _estimate));
                            return;
                        }
                    }

                    _currentEpoch += 1;
                    StartEpoch();
                    return;
                default:
                    throw new InvalidOperationException($"Cannot handle InternalMessage of type {message.GetType()}");
            }
        }

        private void StartEpoch()
        {
            var broadcastId = new BinaryBroadcastId(_agreementId.Era, _agreementId.Agreement, _currentEpoch);
            IBinaryBroadcast broadcast = new BinaryBroadcast(_parties, _faulty, broadcastId, _consensusBroadcaster);
            _dispatcher.RegisterAlgorithm(broadcast, broadcastId);
            broadcast.Input(_estimate);
        }
    }
}