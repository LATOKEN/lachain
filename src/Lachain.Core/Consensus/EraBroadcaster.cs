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
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Proto;
using MessageEnvelope = Lachain.Consensus.Messages.MessageEnvelope;

namespace Lachain.Core.Consensus
{
    /**
     * Stores and dispatches messages between set of protocol of one player within one era (block)
     */
    public class EraBroadcaster : IConsensusBroadcaster
    {
        private readonly ILogger<EraBroadcaster> _logger = LoggerFactory.GetLoggerForClass<EraBroadcaster>();
        private readonly IValidatorManager _validatorManager;
        private readonly long _era;
        private readonly IMessageDeliverer _messageDeliverer;
        private readonly IMessageFactory _messageFactory;
        private readonly IPrivateWallet _wallet;
        private bool _terminated;

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
            long era, IMessageDeliverer messageDeliverer, IValidatorManager validatorManager,
            IPrivateWallet wallet
        )
        {
            _messageDeliverer = messageDeliverer;
            _messageFactory = new MessageFactory(wallet.EcdsaKeyPair);
            _validatorManager = validatorManager;
            _wallet = wallet;
            _terminated = false;
            _era = era;
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
                _logger.LogDebug($"Era {_era} is already finished, skipping Broadcast");
                return;
            }

            var payload = _messageFactory.ConsensusMessage(message);
            foreach (var publicKey in _validatorManager.GetValidatorsPublicKeys(_era - 1))
            {
                if (publicKey.Equals(_wallet.EcdsaKeyPair.PublicKey))
                {
                    Dispatch(message, GetMyId());
                }
                else
                {
                    _messageDeliverer.SendTo(publicKey, payload);
                }
            }
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            message.Validator = new Validator {Era = _era};
            if (_terminated)
            {
                _logger.LogDebug($"Era {_era} is already finished, skipping SendToValidator");
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
            _messageDeliverer.SendTo(_validatorManager.GetPublicKey((uint) index, _era - 1), payload);
        }

        public void Dispatch(ConsensusMessage message, int from)
        {
            if (_terminated)
            {
                _logger.LogDebug($"Era {_era} is already finished, skipping Dispatch");
                return;
            }

            if (message.Validator.Era != _era)
            {
                throw new InvalidOperationException(
                    $"Message for era {message.Validator.Era} dispatched to era {_era}");
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
                    var reliableBroadcastId = new ReliableBroadcastId(message.ValMessage.AssociatedValidatorId, (int) message.Validator.Era);
                    EnsureProtocol(reliableBroadcastId)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    var rbIdEchoMsg = new ReliableBroadcastId(message.EchoMessage.AssociatedValidatorId, (int) message.Validator.Era);
                    EnsureProtocol(rbIdEchoMsg)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.ReadyMessage:
                    var rbIdReadyMsg = new ReliableBroadcastId(message.ReadyMessage.AssociatedValidatorId, (int) message.Validator.Era);
                    EnsureProtocol(rbIdReadyMsg)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                case ConsensusMessage.PayloadOneofCase.SignedHeaderMessage:
                    var rootId = new RootProtocolId(message.Validator.Era);
                    EnsureProtocol(rootId)?.ReceiveMessage(new MessageEnvelope(message, from));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type {message.PayloadCase}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void InternalRequest<TId, TInputType>(ProtocolRequest<TId, TInputType> request)
            where TId : IProtocolIdentifier
        {
            if (_terminated)
            {
                _logger.LogDebug($"Era {_era} is already finished, skipping InternalRequest");
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

            _logger.LogDebug($"Protocol {request.From} requested result from protocol {request.To}");
            EnsureProtocol(request.To);
            _registry[request.To]?.ReceiveMessage(new MessageEnvelope(request, GetMyId()));
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
            _logger.LogDebug($"Protocol {result.From} returned result");
            if (_terminated)
            {
                _logger.LogDebug($"Era {_era} is already finished, skipping InternalResponse");
                return;
            }

            if (_callback.TryGetValue(result.From, out var senderId))
            {
                if (_registry[senderId] == null)
                {
                    _logger.LogWarning($"There is no protocol registered to get result from {senderId}");
                }

                _registry[senderId]?.ReceiveMessage(new MessageEnvelope(result, GetMyId()));
                _logger.LogDebug($"Result from protocol {result.From} delivered to {senderId}");
            }

            _logger.LogDebug($"Result from protocol {result.From} delivered to itself");
            // message is also delivered to self
            _registry[result.From]?.ReceiveMessage(new MessageEnvelope(result, GetMyId()));
        }

        public int GetMyId()
        {
            return _validatorManager.GetValidatorIndex(_wallet.EcdsaKeyPair.PublicKey, _era - 1);
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        private IConsensusProtocol? EnsureProtocol(IProtocolIdentifier id)
        {
            ValidateId(id);
            if (_registry.TryGetValue(id, out var existingProtocol)) return existingProtocol;
            _logger.LogDebug($"Creating protocol {id} on demand");
            if (_terminated)
            {
                _logger.LogWarning($"Protocol {id} not created since broadcaster is terminated");
                return null;
            }

            var publicKeySet = _validatorManager.GetValidators(_era - 1);
            switch (id)
            {
                case BinaryBroadcastId bbId:
                    var bb = new BinaryBroadcast(bbId, publicKeySet, this);
                    RegisterProtocols(new[] {bb});
                    return bb;
                case CoinId coinId:
                    var coin = new CommonCoin(
                        coinId, publicKeySet,
                        _wallet.GetThresholdSignatureKeyForBlock((ulong) _era - 1) ??
                        throw new InvalidOperationException($"No TS keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {coin});
                    return coin;
                case ReliableBroadcastId rbcId:
                    var rbc = new ReliableBroadcast(rbcId, publicKeySet, this); // TODO: unmock RBC
                    RegisterProtocols(new[] {rbc});
                    return rbc;
                case BinaryAgreementId baId:
                    var ba = new BinaryAgreement(baId, publicKeySet, this);
                    RegisterProtocols(new[] {ba});
                    return ba;
                case CommonSubsetId acsId:
                    var acs = new CommonSubset(acsId, publicKeySet, this);
                    RegisterProtocols(new[] {acs});
                    return acs;
                case HoneyBadgerId hbId:
                    var hb = new HoneyBadger(
                        hbId, publicKeySet,
                        _wallet.GetTpkePrivateKeyForBlock((ulong) _era - 1)
                        ?? throw new InvalidOperationException($"No TPKE keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {hb});
                    return hb;
                case RootProtocolId rootId:
                    var root = new RootProtocol(rootId, publicKeySet, _wallet.EcdsaKeyPair.PrivateKey, this);
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

        public void WaitFinish()
        {
            EnsureProtocol(new RootProtocolId(_era))?.WaitFinish();
        }
    }
}