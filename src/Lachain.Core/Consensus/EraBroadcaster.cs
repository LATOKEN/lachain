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

        private readonly IDictionary<IProtocolIdentifier, List<MessageEnvelope>> _postponedMessages = 
            new ConcurrentDictionary<IProtocolIdentifier, List<MessageEnvelope>>();

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
            foreach (var publicKey in _validators!.EcdsaPublicKeySet)
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
            _consensusMessageDeliverer.SendTo(_validators!.EcdsaPublicKeySet[index], payload);
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

            IProtocolIdentifier protocolId;
            switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    protocolId = new BinaryBroadcastId(message.Validator.Era, message.Bval.Agreement,
                        message.Bval.Epoch);
                    break;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    protocolId = new BinaryBroadcastId(message.Validator.Era, message.Aux.Agreement, message.Aux.Epoch);
                    break;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    protocolId = new BinaryBroadcastId(message.Validator.Era, message.Conf.Agreement,
                        message.Conf.Epoch);
                    break;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    protocolId = new CoinId(message.Validator.Era, message.Coin.Agreement, message.Coin.Epoch);
                    break;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    protocolId = new HoneyBadgerId((int) message.Validator.Era);
                    break;
                case ConsensusMessage.PayloadOneofCase.ValMessage:
                    protocolId = new ReliableBroadcastId(message.ValMessage.SenderId, (int) message.Validator.Era);
                    break;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    protocolId = new ReliableBroadcastId(message.EchoMessage.SenderId, (int) message.Validator.Era);
                    break;
                case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                    protocolId = new ReliableBroadcastId(message.ReadyMessage.SenderId, (int) message.Validator.Era);
                    break;
                case ConsensusMessage.PayloadOneofCase.SignedHeaderMessage:
                    protocolId = new RootProtocolId(message.Validator.Era);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type {message}");
            }

            HandleExternalMessage(protocolId, new MessageEnvelope(message, from));
        }

        private void HandleExternalMessage(IProtocolIdentifier protocolId, MessageEnvelope message)
        {
            // For external message we don't create new protocols. each protocol is requested for some result
            // by another protocol (maybe itself) via internal message. When protocols are created by internal message
            // we deliver the external messages to that protocol, otherwise store them to deliver later
            if (ValidateProtocolId(protocolId))
            {
                if (message.External)
                {
                    IConsensusProtocol? protocol;
                    // lock before getting protocol, otherwise protocol can be created in the meantime
                    // and this particular message will never be delivered
                    lock (_postponedMessages)
                    {
                        protocol = GetProtocolById(protocolId);
                        if (protocol is null)
                        {
                            _postponedMessages
                                .PutIfAbsent(protocolId, new List<MessageEnvelope>())
                                .Add(message);
                        }
                    }
                    if (!(protocol is null)) protocol.ReceiveMessage(message);
                }
                else Logger.LogWarning("Internal message should not be here");
            }
            else
            {
                var from = message.ValidatorIndex;
                Logger.LogWarning($"Invalid protocol id {protocolId} from validator {GetPublicKeyById(from)!.ToHex()} ({from})");
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
            
            if (!(protocol is null))
            {
                lock (_postponedMessages)
                {
                    if (_postponedMessages.TryGetValue(request.To, out var savedMessages))
                    {
                        foreach (var message in savedMessages)
                        {
                            protocol.ReceiveMessage(message);
                        }
                        _postponedMessages.Remove(request.To);
                    }
                }
            }
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
            return _validators!.GetValidatorIndex(publicKey);
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
            lock (_postponedMessages)
            {
                _postponedMessages.Clear();
            }
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
            if (!ValidateProtocolId(id)) return null;
            switch (id)
            {
                case BinaryBroadcastId bbId:
                    var bb = new BinaryBroadcast(bbId, _validators!, this);
                    RegisterProtocols(new[] {bb});
                    return bb;
                case CoinId coinId:
                    var coin = new CommonCoin(
                        coinId, _validators!,
                        _wallet.GetThresholdSignatureKeyForBlock((ulong) _era - 1) ??
                        throw new InvalidOperationException($"No TS keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {coin});
                    return coin;
                case ReliableBroadcastId rbcId:
                    var rbc = new ReliableBroadcast(rbcId, _validators!, this);
                    RegisterProtocols(new[] {rbc});
                    return rbc;
                case BinaryAgreementId baId:
                    var ba = new BinaryAgreement(baId, _validators!, this);
                    RegisterProtocols(new[] {ba});
                    return ba;
                case CommonSubsetId acsId:
                    var acs = new CommonSubset(acsId, _validators!, this);
                    RegisterProtocols(new[] {acs});
                    return acs;
                case HoneyBadgerId hbId:
                    var hb = new HoneyBadger(
                        hbId, _validators!,
                        _wallet.GetTpkePrivateKeyForBlock((ulong) _era - 1)
                        ?? throw new InvalidOperationException($"No TPKE keys present for era {_era}"),
                        !HardforkHeights.IsHardfork_12Active((ulong)_era > 2 * StakingContract.CycleDuration ? (ulong)_era - 2 * StakingContract.CycleDuration : 0), 
                        this
                    );
                    RegisterProtocols(new[] {hb});
                    return hb;
                case RootProtocolId rootId:
                    var root = new RootProtocol(rootId, _validators!, _wallet.EcdsaKeyPair.PrivateKey, 
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

        private bool ValidateProtocolId(IProtocolIdentifier id)
        {
            switch (id)
            {
                case BinaryBroadcastId bbId:
                    return ValidateBinaryBroadcastId(bbId);
                case CoinId coinId:
                    return ValidateCoinId(coinId);
                case ReliableBroadcastId rbcId:
                // need to validate the sender id only
                    return ValidateSenderId((long) rbcId.SenderId);
                case BinaryAgreementId baId:
                // created only by internal request, external messages never reach BinaryAgreementId
                // so no need to validate
                    return true;
                case CommonSubsetId acsId:
                // created only by internal request, external messages never reach BinaryAgreementId
                // so no need to validate
                    return true;
                case HoneyBadgerId hbId:
                // only has era in the fields
                    ValidateId(hbId);
                    return true;
                case RootProtocolId rootId:
                // only has era in the fields
                    ValidateId(rootId);
                    return true;
                default:
                    return false;
            }
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
                var binaryAgreementId = CreateBaId(coinId.Agreement);
                if (!(binaryAgreementId is null) &&
                    _registry.TryGetValue(binaryAgreementId, out var binaryAgreement) &&
                    binaryAgreement.Terminated)
                {
                    Logger.LogInformation($"BinaryAgreement {binaryAgreementId} for CoinId {coinId} is already terminated");
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
                var binaryAgreementId = CreateBaId(binaryBroadcastId.Agreement);
                // Checking if BA is terminated
                if (!(binaryAgreementId is null) &&
                    _registry.TryGetValue(binaryAgreementId, out var binaryAgreement) &&
                    binaryAgreement.Terminated)
                {
                    Logger.LogInformation($"BinaryAgreement {binaryAgreementId} for BinaryBroadcastId " +
                                          $"{binaryBroadcastId} is already terminated");
                    return false;
                }
                
                return true;
            }
            // 0 epoch
            return true;
        }

        private BinaryAgreementId? CreateBaId(long senderId)
        {
            return ValidateSenderId(senderId) ? new BinaryAgreementId(_era, senderId) : null;
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