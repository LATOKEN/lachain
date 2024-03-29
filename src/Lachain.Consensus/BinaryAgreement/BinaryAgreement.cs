﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.Messages;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.BinaryAgreement
{
    public class BinaryAgreement : AbstractProtocol
    {
        private static readonly ILogger<BinaryAgreement> Logger = LoggerFactory.GetLoggerForClass<BinaryAgreement>();

        private readonly BinaryAgreementId _agreementId;
        private bool? _result;
        private ResultStatus _requested;
        private long _currentEpoch;
        private bool _estimate;
        private BoolSet _currentValues;
        private bool _wasRepeat;
        private long _resultEpoch;
        private readonly Dictionary<long, bool> _coins = new Dictionary<long, bool>();
        private readonly Dictionary<long, BoolSet> _binaryBroadcastsResults = new Dictionary<long, BoolSet>();

        public BinaryAgreement(
            BinaryAgreementId agreementId, IPublicConsensusKeySet wallet, IConsensusBroadcaster broadcaster)
            : base(wallet, agreementId, broadcaster)
        {
            _agreementId = agreementId;
            _requested = ResultStatus.NotRequested;
            _currentEpoch = 0;
            _resultEpoch = 0;
            _wasRepeat = false;
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested == ResultStatus.Requested)
            {
                Broadcaster.InternalResponse(
                    new ProtocolResult<BinaryAgreementId, bool>(_agreementId, (bool) _result));
                _requested = ResultStatus.Sent;
                SetResult();
                Logger.LogTrace($"{_agreementId}: made result at Ep={_currentEpoch}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void TryProgressEpoch()
        {
            CheckResult();
            while (_result == null || !_wasRepeat)
            {
                if (_currentEpoch % 2 == 0)
                {
                    // epoch mod 2 = 0 -> we have not yet initiated BB
                    if (_currentEpoch != 0 && !_coins.ContainsKey(_currentEpoch - 1))
                    {
                    /*    Logger.LogTrace(
                            $"{_agreementId}: can't progress epoch, blocked, coin (Ep={_currentEpoch - 1}) not present"); */
                        return; // we cannot progress since coin is not tossed and estimate is not correct
                    }

                /*    Logger.LogTrace(
                        $"Epoch progressed, coin (Ep={_currentEpoch - 1}) is present " +
                        $"with value {_currentEpoch > 0 && _coins[_currentEpoch - 1]}"
                    ); */
                    // we have right to calculate new estimate and proceed
                    if (_currentEpoch != 0)
                    {
                        var s = _coins[_currentEpoch - 1];
                        _estimate = _currentValues.Values().First();

                        if (_currentValues.Count() == 1 && _result == null)
                        {
                            if (_estimate == s)
                            {
                                // we are winners!
                                _resultEpoch = _currentEpoch;
                                _result = _estimate;
                                CheckResult();
                                Logger.LogTrace($"{_agreementId}: result = {_result} achieved at Ep={_currentEpoch}");
                            }
                        }
                        else if (_result == s)
                        {
                            if (_currentEpoch > _resultEpoch)
                            {
                                Logger.LogTrace(
                                    $"{_agreementId}: value repeated at Ep={_currentEpoch}, result is already obtained: {_result}. Terminating protocol"
                                );
                                _wasRepeat = true;
                                Terminate();
                            }
                        }
                        else
                        {
                            _estimate = s;
                        }
                    }

                    if (_result != null)
                        _estimate = _result.Value;

                    // here we start new BB assuming that current estimate is correct
                    var broadcastId = new BinaryBroadcastId(_agreementId.Era, _agreementId.AssociatedValidatorId,
                        _currentEpoch);
                    Broadcaster.InternalRequest(
                        new ProtocolRequest<BinaryBroadcastId, bool>(Id, broadcastId, _estimate)
                    );
                    _currentEpoch += 1;
                }
                else
                {
                    // epoch mod 2 = 1 -> we have not yet tossed coin
                    if (!_binaryBroadcastsResults.ContainsKey(_currentEpoch - 1))
                    {
                    /*    Logger.LogTrace(
                            $"{_agreementId}: can't progress epoch, blocked, BB (Ep={_currentEpoch - 1}) not present"
                        ); */
                        return; // we cannot progress since BB is not completed
                    }

                //    Logger.LogTrace($"{_agreementId}: epoch progressed, BB (Ep={_currentEpoch - 1}) is present");

                    _currentValues = _binaryBroadcastsResults[_currentEpoch - 1];
                    var coinId = new CoinId(_agreementId.Era, _agreementId.AssociatedValidatorId, _currentEpoch);
                    if (CoinToss.CreateCoinId(_currentEpoch))
                    {
                        Broadcaster.InternalRequest(new ProtocolRequest<CoinId, object?>(Id, coinId, null));
                    }
                    else
                    {
                        _coins[_currentEpoch] = CoinToss.TossCoin(_currentEpoch) != 0;
                    }

                    _currentEpoch += 1;
                }
            }
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                Logger.LogError($"{_agreementId}: Binary agreement should not receive external messages");
                _lastMessage = $"{_agreementId}: Binary agreement should not receive external messages";
                throw new InvalidOperationException("Binary agreement should not receive external messages");
            }

            var message = envelope.InternalMessage;
            if (message is null)
            {
                Logger.LogError($"{_agreementId}: Failed to decode internal message");
                _lastMessage = $"{_agreementId}: Failed to decode internal message";
                throw new ArgumentNullException();
            }

            switch (message)
            {
                case ProtocolRequest<BinaryAgreementId, bool> agreementRequested:
                    if (_currentEpoch != 0 || _requested != ResultStatus.NotRequested)
                    {
                        _lastMessage = $"{_agreementId}: Cannot propose value: protocol is already running";
                        break;
                        // TODO: fix back or add some logic to handle parents fault
                        // throw new InvalidOperationException("Cannot propose value: protocol is already running");
                    }

                    _requested = ResultStatus.Requested;
                    _estimate = agreementRequested.Input;
                    Logger.LogTrace(
                        $"{_agreementId}: started BA loop in epoch {_currentEpoch} with initial estimate {_estimate}");
                    _lastMessage = $"{_agreementId}: started BA loop in epoch {_currentEpoch} with initial estimate {_estimate}";
                    TryProgressEpoch();
                    break;
                case ProtocolResult<BinaryAgreementId, bool> _:
                    _lastMessage = $"{_agreementId}: got result";
                    break;
                case ProtocolResult<BinaryBroadcastId, BoolSet> broadcastCompleted:
                {
                    Logger.LogTrace(
                        $"{_agreementId}: broadcast {broadcastCompleted.Id.Epoch} completed at era {Id.Era} with result {broadcastCompleted.Result}"
                    );
                    _lastMessage = $"{_agreementId}: broadcast {broadcastCompleted.Id.Epoch} completed at era {Id.Era} with result {broadcastCompleted.Result}";
                    _binaryBroadcastsResults[broadcastCompleted.Id.Epoch] = broadcastCompleted.Result;
                    TryProgressEpoch();
                    return;
                }
                case ProtocolResult<CoinId, CoinResult> coinTossed:
                    _lastMessage = $"{_agreementId}: coin tossed {coinTossed.Id}";
                    _coins[coinTossed.Id.Epoch] = coinTossed.Result.Parity();
                    if (F == 0)
                    {
                        // if there are no tolerance for faulty player, threshold signature will be also fixed constant
                        // so we can substitute coin with parity of the Epoch
                        _coins[coinTossed.Id.Epoch] = (coinTossed.Id.Epoch / 2) % 2 == 0;
                    }

                    TryProgressEpoch();
                    return;
                default:
                    _lastMessage = $"Cannot handle message of type {message.GetType()}";
                    throw new InvalidOperationException($"Cannot handle message of type {message.GetType()}");
            }
        }
    }
}