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
using Lachain.Core.Blockchain.SystemContracts;
using Lachain.Core.Blockchain.Validators;
using Lachain.Core.Vault;
using Lachain.Networking;
using Lachain.Proto;
using Lachain.Storage.Repositories;
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
            Logger.LogTrace($"Creating protocol {id} on demand");
            if (_terminated)
            {
                Logger.LogTrace($"Protocol {id} not created since broadcaster is terminated");
                return null;
            }

            switch (id)
            {
                case BinaryBroadcastId bbId:
                    var bb = new BinaryBroadcast(bbId, _validators, this);
                    RegisterProtocols(new[] {bb});
                    return bb;
                case CoinId coinId:
                    var coin = new CommonCoin(
                        coinId, _validators,
                        _wallet.GetThresholdSignatureKeyForBlock((ulong) _era - 1) ??
                        throw new InvalidOperationException($"No TS keys present for era {_era}"),
                        this
                    );
                    RegisterProtocols(new[] {coin});
                    return coin;
                case ReliableBroadcastId rbcId:
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
                    var root = new RootProtocol(rootId, _validators, _wallet.EcdsaKeyPair.PrivateKey, this, _validatorAttendanceRepository, StakingContract.CycleDuration);
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

        public bool WaitFinish(TimeSpan timeout)
        {
            return EnsureProtocol(new RootProtocolId(_era))?.WaitFinish(timeout) ?? true;
        }

        public IDictionary<IProtocolIdentifier, IConsensusProtocol> GetRegistry()
        {
            return _registry;
        }
    }
}