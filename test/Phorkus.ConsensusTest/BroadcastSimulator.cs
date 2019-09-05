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
using Phorkus.Proto;

namespace Phorkus.ConsensusTest
{
    public class BroadcastSimulator : IConsensusBroadcaster
    {
        private readonly int _sender;

        private readonly Dictionary<IProtocolIdentifier, IConsensusProtocol> _registry =
            new Dictionary<IProtocolIdentifier, IConsensusProtocol>();

        private readonly Dictionary<IProtocolIdentifier, IProtocolIdentifier> _callback =
            new Dictionary<IProtocolIdentifier, IProtocolIdentifier>();

        private readonly DeliverySerivce _deliverySerivce;

        private readonly IWallet _wallet;

        private readonly ISet<int> _silenced;
        
        public bool MixMessages { get; }

        public BroadcastSimulator(int sender, IWallet wallet, DeliverySerivce deliverySerivce, bool mixMessages)
        {
            _sender = sender;
            _deliverySerivce = deliverySerivce;
            _deliverySerivce.AddPlayer(GetMyId(), this);
            _wallet = wallet;
            _silenced = new HashSet<int>();
            MixMessages = mixMessages;
        }

        public void Silent(int id)
        {
            _silenced.Add(id);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void RegisterProtocols(IEnumerable<IConsensusProtocol> protocols)
        {
            foreach (var protocol in protocols)
            {
                if (_registry.ContainsKey(protocol.Id))
                    throw new InvalidOperationException($"Protocol with id ({protocol.Id}) already registered");
                _registry[protocol.Id] = protocol;
            }
        }

        public void Broadcast(ConsensusMessage message)
        {
            _deliverySerivce.BroadcastMessage(message);
        }

        public void SendToValidator(ConsensusMessage message, int index)
        {
            _deliverySerivce.SendToPlayer(index, message);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void CheckRequest(IProtocolIdentifier id)
        {
            if (_registry.ContainsKey(id)) return;
            Console.Error.WriteLine($"{_sender}: creating protocol {id} on demand.");
            switch (id)
            {
                case BinaryBroadcastId bbId:
                    RegisterProtocols(new[]
                    {
                        new BinaryBroadcast(bbId, _wallet, this)
                    });
                    break;
                case CoinId coinId:
                    RegisterProtocols(new[]
                    {
                        new CommonCoin(coinId, _wallet, this),
                    });
                    break;
                case ReliableBroadcastId rbcId:
                    RegisterProtocols(new []
                    {
                       new MockReliableBroadcast(rbcId, _wallet, this),  
                    });
                    break;
                case BinaryAgreementId baId:
                    RegisterProtocols(new []
                    {
                        new BinaryAgreement(baId, _wallet, this), 
                    });
                    break;
                case CommonSubsetId acsId:
                    RegisterProtocols(new []
                    {
                        new CommonSubset(acsId, _wallet, this), 
                    });
                    break;
                default:
                    throw new Exception($"Unknown protocol type {id}");
            }

            Console.Error.WriteLine($"{_sender}: created protocol {id}.");
        }

    public void Dispatch(ConsensusMessage message)
    {
        if (_silenced.Contains((int) message.Validator.ValidatorIndex))
            return;
        if (_silenced.Contains(GetMyId()))
            return;
        
        switch (message.PayloadCase)
            {
                case ConsensusMessage.PayloadOneofCase.Bval:
                    var idBval = new BinaryBroadcastId(message.Validator.Era, message.Bval.Agreement, message.Bval.Epoch);
                    CheckRequest(idBval);
                    _registry[idBval]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Aux:
                    var idAux = new BinaryBroadcastId(message.Validator.Era, message.Aux.Agreement, message.Aux.Epoch);
                    CheckRequest(idAux);
                    _registry[idAux]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Conf:
                    var idConf = new BinaryBroadcastId(message.Validator.Era, message.Conf.Agreement, message.Conf.Epoch);
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
                case ConsensusMessage.PayloadOneofCase.EncryptedShare:
                    var idEncryptedShare = new ReliableBroadcastId(message.EncryptedShare.Id,(int) message.Validator.Era);
                    CheckRequest(idEncryptedShare);
                    _registry[idEncryptedShare]?.ReceiveMessage(new MessageEnvelope(message));
                    break;
                case ConsensusMessage.PayloadOneofCase.Decrypted:
                    var hbbftId = new HoneyBadgerId((int) message.Validator.Era);
                    CheckRequest(hbbftId);
                    _registry[hbbftId]?.ReceiveMessage(new MessageEnvelope(message));
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
                if (_callback.ContainsKey(request.To))
                    throw new InvalidOperationException(
                        "Cannot have two requests from different protocols to one protocol");
                _callback[request.To] = request.From;
            }

            Console.Error.WriteLine($"Party {GetMyId()} received internal request from {request.From}");
            CheckRequest(request.To);
            _registry[request.To]?.ReceiveMessage(new MessageEnvelope(request));
        }

        public void InternalResponse<TId, TResultType>(ProtocolResult<TId, TResultType> result)
            where TId : IProtocolIdentifier
        {
//            Console.Error.WriteLine($"Player {GetMyId()}: result from {result.From}");
            if (_callback.TryGetValue(result.From, out var senderId))
            {
                _registry[senderId]?.ReceiveMessage(new MessageEnvelope(result));
                Console.Error.WriteLine($"Player {GetMyId()} sent response to batya {senderId}");
            }

            // message is also delivered to self
            _registry[result.From]?.ReceiveMessage(new MessageEnvelope(result));
        }

        public int GetMyId()
        {
            return _sender;
        }
    }
}