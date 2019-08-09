using System;
using System.Collections.Generic;
using Org.BouncyCastle.Asn1.Pkcs;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Proto;

namespace Phorkus.Consensus.HoneyBadger
{
    class HoneyBadger : AbstractProtocol
    {
        // todo investigate where id should come from
        private HoneyBadgerId _honeyBadgerId;
        private ResultStatus _requested;
        private bool[] _taken;

        private IRawShare _rawShare;
        private IEncryptedShare _encryptedShare;
        private IRawShare[] _shares;
        
        private IConsensusBroadcaster _broadcaster;
        private int _n;
        private int _f;
        private ISet<IRawShare> _result;

        private ISet<IPartiallyDecryptedShare>[] _decryptedShares;

        private TPKEPubKey _pubKey;
        private TPKEPrivKey _privKey;

        public HoneyBadger(int n, int f, IConsensusBroadcaster broadcaster)
        {
            _broadcaster = broadcaster;
            _n = n;
            _f = f;
            
            _decryptedShares = new HashSet<IPartiallyDecryptedShare>[_n];
            for (var i = 0; i < _n; ++i)
            {
                _decryptedShares[i] = new HashSet<IPartiallyDecryptedShare>();
            }
            
            _taken = new bool[_n];
            _shares = new IRawShare[_n];
            _result = new HashSet<IRawShare>();
            _requested = ResultStatus.NotRequested;
        }
        
        public override IProtocolIdentifier Id { get; }
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Dec:
                        HandleDecMessage(message.Validator, message.Dec);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to CommonSubset protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<HoneyBadgerId, IRawShare> commonSubsetRequested:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast requested");
                        HandleInputMessage(commonSubsetRequested);
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> _:
//                        Console.Error.WriteLine($"{_consensusBroadcaster.GetMyId()}: broadcast completed");
                        Terminated = true;
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<IEncryptedShare>> result:
                        HandleCommonSubset(result);
                        break;
                    case ProtocolResult<TPKESetupId, Tuple<TPKEPubKey, TPKEPrivKey>> response:
                        HandleTPKESetup(response);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleDecMessage(Validator messageValidator, DecMessage messageDec)
        {
            IPartiallyDecryptedShare share = _pubKey.Decode(messageDec);
            _decryptedShares[share.Id].Add(share);
            CheckDecryptedShares(share.Id);
        }

        private void CheckDecryptedShares(int id)
        {
            if (!_taken[id]) return;
            if (_decryptedShares[id].Count < _f + 1) return;
            if (_shares[id] != null) return;
            _shares[id] = _pubKey.FullDecrypt(_decryptedShares[id]);

            CheckAllSharesDecrypted();
        }

        private void CheckAllSharesDecrypted()
        {
            if (_result != null) return;
            
            for (var i = 0; i < _n; ++i)
                if (_taken[i] && _shares[i] == null)
                    return;
            
            for (var i = 0; i < _n; ++i)
                if (_taken[i])
                    _result.Add(_shares[i]);

            CheckResult();
        }
        
        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _requested = ResultStatus.Sent;
            _broadcaster.InternalResponse(
                new ProtocolResult<HoneyBadgerId, ISet<IRawShare>>(_honeyBadgerId, _result));
        }

        private void HandleTPKESetup(ProtocolResult<TPKESetupId, Tuple<TPKEPubKey, TPKEPrivKey>> response)
        {
            _pubKey = response.Result.Item1;
            _privKey = response.Result.Item2;

            CheckEncryption();
        }

        private void HandleInputMessage(ProtocolRequest<HoneyBadgerId, IRawShare> request)
        {
            _rawShare = request.Input;
            _requested = ResultStatus.Requested;
            
            _broadcaster.InternalRequest(new ProtocolRequest<TPKESetupId, Object>(Id, new TPKESetupId(_honeyBadgerId), null));

            CheckEncryption();
            CheckResult();
        }

        private void CheckEncryption()
        {
            if (_pubKey == null) return;
            if (_rawShare == null) return;
            if (_encryptedShare != null) return;
            _encryptedShare = _pubKey.Encrypt(_rawShare);
            _broadcaster.InternalRequest(new ProtocolRequest<CommonSubsetId, IEncryptedShare>(Id, new CommonSubsetId(_honeyBadgerId), _encryptedShare));
        }

        private void HandleCommonSubset(ProtocolResult<CommonSubsetId, ISet<IEncryptedShare>> result)
        {
            foreach (IEncryptedShare share in result.Result)
            {
                var dec = _privKey.Decrypt(share);
                _taken[share.Id] = true;
                _decryptedShares[share.Id].Add(dec);
                _broadcaster.Broadcast(CreateDecMessage(dec));
                CheckDecryptedShares(share.Id);
            }
            throw new NotImplementedException();
        }

        private ConsensusMessage CreateDecMessage(IPartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
               Validator = new Validator
               {
                   ValidatorIndex = _broadcaster.GetMyId(),
                   Era = _honeyBadgerId.Era
               },
               Dec = _pubKey.Encode(share)
            };
            return message;
        }
    }
}