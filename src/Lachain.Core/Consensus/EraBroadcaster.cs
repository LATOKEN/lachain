using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Lachain.Logger;
using Lachain.Consensus;
using Lachain.Consensus.BinaryAgreement;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Consensus.ReliableBroadcast;
using Lachain.Consensus.RootProtocol;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Utils;
using MessageEnvelope = Lachain.Consensus.Messages.MessageEnvelope;

namespace Lachain.Core.Consensus
{
    /**
     * Stores and dispatches messages between set of protocol of one player within one era (block)
     */
    public class EraBroadcaster : IConsensusBroadcaster
    {
        private static readonly ILogger<EraBroadcaster> Logger = LoggerFactory.GetLoggerForClass<EraBroadcaster>();
        
        private readonly long _era;
        private readonly IConsensusMessageDeliverer _consensusMessageDeliverer;
        private readonly IMessageFactory _messageFactory;
        private readonly IPrivateWallet _wallet;
        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private bool _terminated;
        private int _myIdx;
        private IPublicConsensusKeySet? _validators;
        private long[] _minEpochCC;
        private long[] _minEpochBb;
        private const int _maxAllowedBbId = 10;
        private const int _maxAllowedCoinId = 10;

        public bool Ready => _validators != null;

        /**
         * Registered callbacks, identifying that one protocol requires result from another
         */
        private readonly IDictionary<IProtocolIdentifier, IProtocolIdentifier> _callback =
            new ConcurrentDictionary<IProtocolIdentifier, IProtocolIdentifier>();

        /**
         * Registry of all protocols for this era
         */
        private readonly IDictionary<IProtocolIdentifier, IConsensusProtocol> _registry =
            new ConcurrentDictionary<IProtocolIdentifier, IConsensusProtocol>();

        public EraBroadcaster(
            long era, IConsensusMessageDeliverer consensusMessageDeliverer,
            IPrivateWallet wallet, IValidatorAttendanceRepository validatorAttendanceRepository
        )
        {
            _consensusMessageDeliverer = consensusMessageDeliverer;
            _messageFactory = new MessageFactory(wallet.EcdsaKeyPair);
            _wallet = wallet;
            _terminated = false;
            _era = era;
            _myIdx = -1;
            _validatorAttendanceRepository = validatorAttendanceRepository;
        }

        public void SetValidatorKeySet(IPublicConsensusKeySet keySet)
        {
            _validators = keySet;
            _myIdx = _validators.GetValidatorIndex(_wallet.EcdsaKeyPair.PublicKey);
            InitializeCounter();
        }

        private void InitializeCounter()
        {
            if (_validators is null)
                throw new Exception("We don't have validators");
            _minEpochBb = new long[_validators.N];
            _minEpochCC = new long[_validators.N];
            for (int i = 0; i < _validators.N; i++)
                _minEpochCC[i] = 5;
        }

        public void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols)
        {
            foreach (var protocol in protocols)
            {
                _registry[protocol.Id] = protocol;
            }
        }

        public void Broadcast(ConsensusMessage message)
        {
            message.Validator = new Validator {Era = _era};
            if (_terminated)
            {
                Logger.LogTrace($"Era {_era} is already finished, skipping Broadcast");
                return;
            }

            var payload = _messageFactory.ConsensusMessage(message);
            foreach (var publicKey in _validators.EcdsaPublicKeySet)
            {
                if (publicKey.Equals(_wallet.EcdsaKeyPair.PublicKey))
                {
                    Dispatch(message, GetMyId());
                }
                else
                {
                    _consensusMessageDeliverer.SendTo(publicKey, payload);
                }
            }
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            message.Validator = new Validator {Era = _era};
            if (_terminated)
            {
                Logger.LogTrace($"Era {_era} is already finished, skipping SendToValidator");
                return;
            }

            if (index < 0)
            {
                throw new ArgumentException("Validator index must be positive", nameof(index));
            }

            if (index == GetMyId())
            {
                Dispatch(message, index);
                return;
            }

            var payload = _messageFactory.ConsensusMessage(message);
            _consensusMessageDeliverer.SendTo(_validators.EcdsaPublicKeySet[index], payload);
        }

        public void Dispatch(ConsensusMessage message, int from)
        {
            if (_terminated)
            {
                Logger.LogTrace($"Era {_era} is already finished, skipping Dispatch");
                return;
            }

            if (message.Validator.Era != _era)
            {
                throw new InvalidOperationException(
                    $"Message for era {message.Validator.Era} dispatched to era {_era}");
            }

            if (!ValidateMessage(message))
            {
                Logger.LogWarning(
                    $"Faulty behaviour: null value in required field of the consensus message from validator: {from}"
                    + $" ({GetPublicKeyById(from)!.ToHex()}). Discarding message as it could crash targeted protocol.");
                return;
            }

            switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    var idBval = new BinaryBroadcastId(message.Validator.Era, message.Bval.Agreement,
                        message.Bval.Epoch);
                    EnsureProtocol(idBval)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    var idAux = new BinaryBroadcastId(message.Validator.Era, message.Aux.Agreement, message.Aux.Epoch);
                    EnsureProtocol(idAux)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    var idConf = new BinaryBroadcastId(message.Validator.Era, message.Conf.Agreement,
                        message.Conf.Epoch);
                    EnsureProtocol(idConf)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    var idCoin = new CoinId(message.Validator.Era, message.Coin.Agreement, message.Coin.Epoch);
                    EnsureProtocol(idCoin)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    var hbbftId = new HoneyBadgerId((int) message.Validator.Era);
                    EnsureProtocol(hbbftId)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.ValMessage:
                    var reliableBroadcastId = new ReliableBroadcastId(message.ValMessage.SenderId, (int) message.Validator.Era);
                    EnsureProtocol(reliableBroadcastId)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    var rbIdEchoMsg = new ReliableBroadcastId(message.EchoMessage.SenderId, (int) message.Validator.Era);
                    EnsureProtocol(rbIdEchoMsg)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                    var rbIdReadyMsg = new ReliableBroadcastId(message.ReadyMessage.SenderId, (int) message.Validator.Era);
                    EnsureProtocol(rbIdReadyMsg)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.SignedHeaderMessage:
                    var rootId = new RootProtocolId(message.Validator.Era);
                    EnsureProtocol(rootId)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type {message}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void InternalRequest<TId, TInputType>(ProtocolRequest<TId, TInputType> request)
            where TId : IProtocolIdentifier
        {
            if (_terminated)
            {
                Logger.LogTrace($"Era {_era} is already finished, skipping InternalRequest");
                return;
            }

            if (request.From != null)
            {
                if (_callback.TryGetValue(request.To, out var existingCallback))
                {
                    throw new InvalidOperationException(
                        $"Cannot have two requests from different protocols ({request.From}, " +
                        $"{existingCallback}) to one protocol {request.To}"
                    );
                }

                _callback[request.To] = request.From;
            }

            Logger.LogTrace($"Protocol {request.From} requested result from protocol {request.To}");
            EnsureProtocol(request.To);
            
            if (_registry.TryGetValue(request.To, out var protocol))
                protocol?.ReceiveMessage(new MessageEnvelope(request, GetMyId()));
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
            Logger.LogTrace($"Protocol {result.From} returned result");
            if (_terminated)
            {
                Logger.LogTrace($"Era {_era} is already finished, skipping InternalResponse");
                return;
            }

            if (_callback.TryGetValue(result.From, out var senderId))
            {
                if (!_registry.TryGetValue(senderId, out var cbProtocol))
                {
                    Logger.LogWarning($"There is no protocol registered to get result from {senderId}");
                }
                else
                {
                    cbProtocol?.ReceiveMessage(new MessageEnvelope(result, GetMyId()));
                    Logger.LogTrace($"Result from protocol {result.From} delivered to {senderId}");
                }
            }

            // message is also delivered to self
        //    Logger.LogTrace($"Result from protocol {result.From} delivered to itself");
            if (_registry.TryGetValue(result.From, out var protocol))
                protocol?.ReceiveMessage(new MessageEnvelope(result, GetMyId()));
        }

        public int GetMyId()
        {
            return _myIdx;
        }

        public int GetIdByPublicKey(ECDSAPublicKey publicKey)
        {
            return _validators.GetValidatorIndex(publicKey);
        }

        public ECDSAPublicKey? GetPublicKeyById(int id)
        {
            if (_validators is null || id < 0 || id >= _validators.N) return null;
            return _validators.EcdsaPublicKeySet[id];
        }

        public IConsensusProtocol? GetProtocolById(IProtocolIdentifier id)
        {
            return _registry.TryGetValue(id, out var value) ? value : null;
        }

        public void Terminate()
        {
            if (_terminated) return;
            _terminated = true;
            foreach (var protocol in _registry)
            {
                protocol.Value.Terminate();
            }

            _registry.Clear();
            _callback.Clear();
        }

        // Each ProtocolId is created only once to prevent spamming, Protocols are mapped against ProtocolId, so each
        // Protocol will also be created only once, after achieving result, Protocol terminate and no longer process any
        // messages. 
        [MethodImpl(MethodImplOptions.Synchronized)]
        private IConsensusProtocol? EnsureProtocol(IProtocolIdentifier id)
        {
            ValidateId(id);
            if (_registry.TryGetValue(id, out var existingProtocol)) return existingProtocol;
            if (_terminated)
            {
                Logger.LogTrace($"Protocol {id} not created since broadcaster is terminated");
                return null;
            }

            var protocol = CreateProtocol(id);
            if (!(protocol is null))
                Logger.LogTrace($"Created protocol {id} on demand");
            return protocol;
        }

        private IConsensusProtocol? CreateProtocol(IProtocolIdentifier id)
        {
            switch (id)
            {
                case BinaryBroadcastId bbId:
                    if (!ValidateBinaryBroadcastId(bbId))
                        return null;
                    var bb = new BinaryBroadcast(bbId, _validators, this);
                    RegisterProtocols(new[] {bb});
                    return bb;
                case CoinId coinId:
                    if (!ValidateCoinId(coinId))
                        return null;
                    var coin = new CommonCoin(
                        coinId, _validators,
                        _wallet.GetThresholdSignatureKeyForBlock((ulong) _era - 1) ??
                        throw new InvalidOperationException($"No TS keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {coin});
                    return coin;
                case ReliableBroadcastId rbcId:
                    if (!ValidateSenderId((long) rbcId.SenderId))
                        return null;
                    var rbc = new ReliableBroadcast(rbcId, _validators, this);
                    RegisterProtocols(new[] {rbc});
                    return rbc;
                case BinaryAgreementId baId:
                    var ba = new BinaryAgreement(baId, _validators, this);
                    RegisterProtocols(new[] {ba});
                    return ba;
                case CommonSubsetId acsId:
                    var acs = new CommonSubset(acsId, _validators, this);
                    RegisterProtocols(new[] {acs});
                    return acs;
                case HoneyBadgerId hbId:
                    var hb = new HoneyBadger(
                        hbId, _validators,
                        _wallet.GetTpkePrivateKeyForBlock((ulong) _era - 1)
                        ?? throw new InvalidOperationException($"No TPKE keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {hb});
                    return hb;
                case RootProtocolId rootId:
                    var root = new RootProtocol(rootId, _validators, _wallet.EcdsaKeyPair.PrivateKey, 
                        this, _validatorAttendanceRepository, StakingContract.CycleDuration,
                        HardforkHeights.IsHardfork_9Active((ulong)_era));
                    RegisterProtocols(new[] {root});
                    return root;
                default:
                    throw new Exception($"Unknown protocol type {id}");
            }
        }

        private void ValidateId(IProtocolIdentifier id)
        {
            if (id.Era != _era)
                throw new InvalidOperationException($"Era mismatched, expected {_era} got message with {id.Era}");
        }
        
        // There are separate instance of ReliableBroadcast for each validator.
        // Check if the SenderId is one of the validator's id before creating ReliableBroadcastId
        // Sender id is basically validator's id, so it must be between 0 and N-1 inclusive
        private bool ValidateSenderId(long senderId)
        {
            if (_validators is null)
            {
                Logger.LogWarning("We don't have validators");
                return false;
            }
            if (senderId < 0 || senderId >= _validators.N)
            {
                Logger.LogWarning($"Invalid sender id in consensus message: {senderId}. N: {_validators.N}");
                return false;
            }
            return true;
        }

        // Check if parent protocol is terminated
        // Check validity
        // Check if non-terminated protocols are not too many
        // Yet this could be exploited in this way: an honest node process CC sequentially, CC-1, CC-3, CC-5,...,
        // Lets say we have CC-11 and we are allowing at most 10 non-terminated CC. A malicious validator can
        // propose for CC-671, CC-677, .. (10 CC like this) together. They will be allowed and maybe never terminated
        // because they are too far. So a proposal from honest validator will be rejected. 
        // We can handle the proposal sequenitally, but due to network latency, messages from other validators may not
        // reach us sequenitally. So we propose this idea: allow max 'X' (for now X = 10) non-terminated CC and also
        // check that: for the lowest non-terminated CC (CC-L) and the highest non-termianted CC (CC-H) are such that
        // the total number of valid CC in between CC-L and CC-H (including) is at most 'X'.
        private bool ValidateCoinId(CoinId coinId)
        {
            if (coinId.Agreement == -1 && coinId.Epoch == 0)
            {
                // This type of coinId is created from RootProtocol or via network from another validator
                return true;
            }
            if (ValidateSenderId(coinId.Agreement))
            {
                // BinaryAgreement requests such CommonCoin
                // Checking if the epoch argument is valid
                var createCoinId = CoinToss.CreateCoinId(coinId.Epoch);
                if (!createCoinId)
                {
                    Logger.LogInformation($"Invalid CoinId: {coinId}");
                    return false;
                }
                // Checking if BA is terminated
                if (_callback.TryGetValue(coinId, out var binaryAgreementId) &&
                    _registry.TryGetValue(binaryAgreementId, out var binaryAgreement) &&
                    binaryAgreement.Terminated)
                {
                    Logger.LogInformation($"BinaryAgreement {binaryAgreementId} for CoinId {coinId} is already terminated");
                    return false;
                }
                // Allow creation of new CC if not too many are present
                var minEpoch = _minEpochCC[coinId.Agreement];
                var minCoinId = new CoinId(coinId.Era, coinId.Agreement, minEpoch);
                while (_registry.TryGetValue(minCoinId, out var minCommonCoin) && minCommonCoin.Terminated)
                {
                    minEpoch = CoinToss.NextCoinCreationEpoch(minEpoch);
                    minCoinId = new CoinId(coinId.Era, coinId.Agreement, minEpoch);
                }

                _minEpochCC[coinId.Agreement] = minEpoch;
                var diff = CoinToss.TotalValidCommonCoin(minEpoch, coinId.Epoch);
                if (diff < 0 || diff > _maxAllowedCoinId)
                {
                    Logger.LogInformation($"Too many CoinId created, request CoinId: {coinId}, non terminated CoinId of minimum "
                                          + $"epoch: {minEpoch}, max CoinId allowed {_maxAllowedCoinId}");
                    return false;
                }
                return true;
            }
            return false;
        }

        // same logic as ValidateCoinId
        private bool ValidateBinaryBroadcastId(BinaryBroadcastId binaryBroadcastId)
        {
            if (!ValidateSenderId(binaryBroadcastId.Agreement) 
                || binaryBroadcastId.Epoch < 0 || (binaryBroadcastId.Epoch & 1) != 0)
            {
                Logger.LogInformation($"Invalid BbId: {binaryBroadcastId}");
                return false;
            }
            if (binaryBroadcastId.Epoch > 0) // positive and even
            {
                // Checking if BA is terminated
                if (_callback.TryGetValue(binaryBroadcastId, out var binaryAgreementId) &&
                    _registry.TryGetValue(binaryAgreementId, out var binaryAgreement) &&
                    binaryAgreement.Terminated)
                {
                    Logger.LogInformation($"BinaryAgreement {binaryAgreementId} for BinaryBroadcastId " +
                                          $"{binaryBroadcastId} is already terminated");
                    return false;
                }
                // Allow creation of new BB if not too many are present
                var minEpoch = _minEpochBb[binaryBroadcastId.Agreement];
                var minBbId = new BinaryBroadcastId(binaryBroadcastId.Era, binaryBroadcastId.Agreement, minEpoch);
                while (_registry.TryGetValue(minBbId, out var minBb) && minBb.Terminated)
                {
                    minEpoch += 2;
                    minBbId = new BinaryBroadcastId(binaryBroadcastId.Era, binaryBroadcastId.Agreement, minEpoch);
                }

                _minEpochBb[binaryBroadcastId.Agreement] = minEpoch;
                var diff = (binaryBroadcastId.Epoch - minEpoch) / 2 + 1;
                if (diff > _maxAllowedBbId)
                {
                    Logger.LogInformation($"Too many BbId created, request BbId: {binaryBroadcastId}, non terminated"
                                          + $" BbId of minimum epoch: {minEpoch}, max BbId allowed {_maxAllowedBbId}");
                    return false;
                }
                return true;
            }
            // 0 epoch
            return true;
        }

        public bool WaitFinish(TimeSpan timeout)
        {
            return EnsureProtocol(new RootProtocolId(_era))?.WaitFinish(timeout) ?? true;
        }

        public IDictionary<IProtocolIdentifier, IConsensusProtocol> GetRegistry()
        {
            return _registry;
        }

        // Some protocols may stop due to null value in some of fields in the message. We discard such cases
        private bool ValidateMessage(ConsensusMessage message)
        {
            switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    // all fields have default values
                    return true;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    // all fields have default values
                    return true;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    // all fields have default values
                    return true;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    // all fields have default values
                    return true;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    // all fields have default values
                    return true;
                case ConsensusMessage.PayloadOneofCase.ValMessage:
                    // merkleTreeRoot cannot be null
                    if (message.ValMessage.MerkleTreeRoot is null) 
                        return false;
                    return true;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    // merkleTreeRoot cannot be null
                    if (message.EchoMessage.MerkleTreeRoot is null) 
                        return false;
                    return true;
                case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                    // merkleTreeRoot cannot be null
                    if (message.ReadyMessage.MerkleTreeRoot is null) 
                        return false;
                    return true;
                case ConsensusMessage.PayloadOneofCase.SignedHeaderMessage:
                    // BlockHeader or Signature cannot be null
                    var header = message.SignedHeaderMessage.Header;
                    if (header is null || message.SignedHeaderMessage.Signature is null)
                        return false;
                    // Check fields of BlockHeader: UInt256 cannot be null
                    if (header.MerkleRoot is null || header.PrevBlockHash is null || header.StateHash is null)
                        return false;
                    return true;
                default:
                    return false;
            }
        }
    }
}