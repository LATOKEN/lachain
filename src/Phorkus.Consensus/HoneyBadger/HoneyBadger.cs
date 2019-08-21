using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly bool[] _taken;

        private IRawShare _rawShare;
        private EncryptedShare _encryptedShare;
        private readonly IRawShare[] _shares;

        private readonly EncryptedShare[] _receivedShares;
        
        private ISet<IRawShare> _result;
        
        
        private readonly ISet<PartiallyDecryptedShare>[] _decryptedShares;

        private TPKEPubKey _pubKey;
        private TPKEPrivKey _privKey;
        private TPKEVerificationKey _verificationKey;

        public override IProtocolIdentifier Id => _honeyBadgerId;

        public HoneyBadger(HoneyBadgerId honeyBadgerId, IWallet wallet, IConsensusBroadcaster broadcaster) : base(wallet, broadcaster)
        {
            _honeyBadgerId = honeyBadgerId;
            
            _receivedShares = new EncryptedShare[N];
            _decryptedShares = new ISet<PartiallyDecryptedShare>[N];
            for (var i = 0; i < N; ++i)
            {
                _decryptedShares[i] = new HashSet<PartiallyDecryptedShare>();
            }
            
            _taken = new bool[N];
            _shares = new IRawShare[N];
            _result = new HashSet<IRawShare>();
            _requested = ResultStatus.NotRequested;
        }
        
        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Decrypted:
                        HandleDecMessage(message.Validator, message.Decrypted);
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
                    case ProtocolRequest<HoneyBadgerId, IRawShare> honeyBadgerRequested:
                        HandleInputMessage(honeyBadgerRequested);
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> _:
                        Terminated = true;
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result:
                        HandleCommonSubset(result);
                        break;
                    case ProtocolResult<TPKESetupId, TPKEKeys> response:
                        HandleTPKESetup(response);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "CommonSubset protocol failed to handle internal message");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<HoneyBadgerId, IRawShare> request)
        {
            _rawShare = request.Input;
            _requested = ResultStatus.Requested;
            
            _broadcaster.InternalRequest(new ProtocolRequest<TPKESetupId, Object>(Id, new TPKESetupId(_honeyBadgerId), null));

            CheckEncryption();
            CheckResult();
        }
        
        private void HandleTPKESetup(ProtocolResult<TPKESetupId, TPKEKeys> response)
        {
            _pubKey = response.Result.PubKey;
            _privKey = response.Result.PrivKey;
            _verificationKey = response.Result.VerificationKey;

            CheckEncryption();
        }
       
        private void CheckEncryption()
        {
            if (_pubKey == null) return;
            if (_rawShare == null) return;
            if (_encryptedShare != null) return;
            _encryptedShare = _pubKey.Encrypt(_rawShare);
            _broadcaster.InternalRequest(new ProtocolRequest<CommonSubsetId, EncryptedShare>(Id, new CommonSubsetId(_honeyBadgerId), _encryptedShare));
        }

        private void HandleCommonSubset(ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result)
        {
            foreach (EncryptedShare share in result.Result)
            {
                var dec = _privKey.Decrypt(share);
                _taken[share.Id] = true;
                _receivedShares[share.Id] = share;
                _decryptedShares[share.Id].Add(dec);
                _broadcaster.Broadcast(message: CreateDecMessage(dec));
                CheckDecryptedShares(share.Id);
            }
            
            CheckResult();
        }

        private ConsensusMessage CreateDecMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
               Validator = new Validator
               {
                   ValidatorIndex = (ulong) GetMyId(),
                   Era = _honeyBadgerId.Era
               },
               Decrypted =_pubKey.Encode(share)
            };
            return message;
        }
        
        private void HandleDecMessage(Validator messageValidator, TPKEPartiallyDecryptedShareMsg msg)
        {
            PartiallyDecryptedShare share = _pubKey.Decode(msg);
            if (!_verificationKey.Verify(_receivedShares[share.DecryptorId], share)) return;
            // possible fault evidence
            _decryptedShares[share.DecryptorId].Add(share);
            CheckDecryptedShares(share.DecryptorId);
        }

        private void CheckDecryptedShares(int id)
        {
            if (!_taken[id]) return;
            if (_decryptedShares[id].Count < F + 1) return;
            if (_shares[id] != null) return;
            _shares[id] = _pubKey.FullDecrypt(_receivedShares[id], _decryptedShares[id].ToList());

            CheckAllSharesDecrypted();
        }

        private void CheckAllSharesDecrypted()
        {
            if (_result != null) return;

            for (var i = 0; i < N; ++i)
                if (_taken[i] && _shares[i] == null)
                    return;
            
            _result = new HashSet<IRawShare>();
            
            for (var i = 0; i < N; ++i)
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

    }
}