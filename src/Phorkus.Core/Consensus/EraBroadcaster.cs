using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Phorkus.Consensus;
using Phorkus.Consensus.BinaryAgreement;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.ReliableBroadcast;
using Phorkus.Consensus.TPKE;
using Phorkus.Core.Blockchain;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Networking;
using Phorkus.Proto;
using MessageEnvelope = Phorkus.Consensus.Messages.MessageEnvelope;

namespace Phorkus.Core.Consensus
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
        private readonly IWallet _wallet;
        private readonly KeyPair _keyPair;
        private bool _terminated;

        /**
         * Registered callbacks, identifying that one protocol requires result from another
         */
        private readonly Dictionary<IProtocolIdentifier, IProtocolIdentifier> _callback =
            new Dictionary<IProtocolIdentifier, IProtocolIdentifier>();

        /**
         * Registry of all protocols for this era
         */
        private readonly Dictionary<IProtocolIdentifier, IConsensusProtocol> _registry =
            new Dictionary<IProtocolIdentifier, IConsensusProtocol>();

        public EraBroadcaster(
            long era, IMessageDeliverer messageDeliverer, IValidatorManager validatorManager,
            KeyPair keyPair, IWallet wallet, ICrypto crypto
        )
        {
            _messageDeliverer = messageDeliverer;
            _messageFactory = new MessageFactory(keyPair);
            _validatorManager = validatorManager;
            _keyPair = keyPair;
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
            var payload = _messageFactory.ConsensusMessage(message);
            foreach (var publicKey in _validatorManager.Validators)
            {
                _messageDeliverer.SendTo(publicKey, payload);
            }
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            if (index < 0)
            {
                throw new ArgumentException("Validator index must be positive", nameof(index));
            }

            var payload = _messageFactory.ConsensusMessage(message);
            _messageDeliverer.SendTo(_validatorManager.GetPublicKey((uint) index), payload);
        }

        public void Dispatch(ConsensusMessage message)
        {
            switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    var idBval = new BinaryBroadcastId(message.Validator.Era, message.Bval.Agreement,
                        message.Bval.Epoch);
                    CheckRequest(idBval);
                    _registry[idBval]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    var idAux = new BinaryBroadcastId(message.Validator.Era, message.Aux.Agreement, message.Aux.Epoch);
                    CheckRequest(idAux);
                    _registry[idAux]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    var idConf = new BinaryBroadcastId(message.Validator.Era, message.Conf.Agreement,
                        message.Conf.Epoch);
                    CheckRequest(idConf);
                    _registry[idConf]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Coin:
                    var idCoin = new CoinId(message.Validator.Era, message.Coin.Agreement, message.Coin.Epoch);
                    CheckRequest(idCoin);
                    _registry[idCoin]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.TpkeKeys:
                    var idTpkeKeys = new TPKESetupId((int) message.Validator.Era);
                    CheckRequest(idTpkeKeys);
                    _registry[idTpkeKeys]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.PolynomialValue:
                    var idPolynomialValue = new TPKESetupId((int) message.Validator.Era);
                    CheckRequest(idPolynomialValue);
                    _registry[idPolynomialValue]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.HiddenPolynomial:
                    var idHiddenPolynomial = new TPKESetupId((int) message.Validator.Era);
                    CheckRequest(idHiddenPolynomial);
                    _registry[idHiddenPolynomial]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.ConfirmationHash:
                    var idConfirmationHash = new TPKESetupId((int) message.Validator.Era);
                    CheckRequest(idConfirmationHash);
                    _registry[idConfirmationHash]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    var hbbftId = new HoneyBadgerId((int) message.Validator.Era);
                    CheckRequest(hbbftId);
                    _registry[hbbftId]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.ValMessage:
                    var reliableBroadcastId = new ReliableBroadcastId((int) message.Validator.ValidatorIndex,
                        (int) message.Validator.Era);
                    CheckRequest(reliableBroadcastId);
                    _registry[reliableBroadcastId]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.EchoMessage:
                    var rbIdEchoMsg = new ReliableBroadcastId((int) message.Validator.ValidatorIndex,
                        (int) message.Validator.Era);
                    CheckRequest(rbIdEchoMsg);
                    _registry[rbIdEchoMsg]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.EncryptedShare:
                    var idEncryptedShare =
                        new ReliableBroadcastId(message.EncryptedShare.Id, (int) message.Validator.Era);
                    CheckRequest(idEncryptedShare);
                    _registry[idEncryptedShare]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown message type {message.PayloadCase}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void InternalRequest<TId, TInputType>(ProtocolRequest<TId, TInputType> request)
            where TId : IProtocolIdentifier
        {
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
            CheckRequest(request.To);
            _registry[request.To]?.ReceiveMessage(new MessageEnvelope(request));
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
            _logger.LogDebug($"Protocol {result.From} returned result");
            if (_callback.TryGetValue(result.From, out var senderId))
            {
                _registry[senderId]?.ReceiveMessage(new MessageEnvelope(result));
                _logger.LogDebug($"Result from protocol {result.From} delivered to {senderId}");
            }

            _logger.LogDebug($"Result from protocol {result.From} delivered to itself");
            // message is also delivered to self
            _registry[result.From]?.ReceiveMessage(new MessageEnvelope(result));
        }

        public int GetMyId()
        {
            return (int) _validatorManager.GetValidatorIndex(_keyPair.PublicKey);
        }

        public IConsensusProtocol GetProtocolById(IProtocolIdentifier id)
        {
            return _registry.TryGetValue(id, out var value) ? value : null;
        }

        public void Terminate()
        {
            _terminated = true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void CheckRequest(IProtocolIdentifier id)
        {
            ValidateId(id);
            if (_registry.ContainsKey(id)) return;
            _logger.LogDebug($"Creating protocol {id} on demand");
            if (_terminated)
            {
                _logger.LogWarning($"Protocol {id} not created since broadcaster is terminated");
                return;
            }

            switch (id)
            {
                case BinaryBroadcastId bbId:
                    RegisterProtocols(new[] {new BinaryBroadcast(bbId, _wallet, this)});
                    break;
                case CoinId coinId:
                    RegisterProtocols(new[] {new CommonCoin(coinId, _wallet, this)});
                    break;
                case ReliableBroadcastId rbcId:
                    RegisterProtocols(new[] {new ReliableBroadcast(rbcId, _wallet, this)});
                    break;
                case BinaryAgreementId baId:
                    RegisterProtocols(new[] {new BinaryAgreement(baId, _wallet, this)});
                    break;
                case CommonSubsetId acsId:
                    RegisterProtocols(new[] {new CommonSubset(acsId, _wallet, this)});
                    break;
                default:
                    throw new Exception($"Unknown protocol type {id}");
            }

            _logger.LogDebug($"Created protocol {id}");
        }

        private void ValidateId(IProtocolIdentifier id)
        {
            if (id.Era != _era)
                throw new InvalidOperationException($"Era mismatched, expected {_era} got message with {id.Era}");
        }
    }
}