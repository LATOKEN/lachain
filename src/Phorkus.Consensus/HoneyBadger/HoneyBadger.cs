﻿using System;
using System.Collections.Generic;
using System.Linq;
using Phorkus.Consensus.CommonSubset;
using Phorkus.Consensus.Messages;
using Phorkus.Crypto.TPKE;
using Phorkus.Logger;
using Phorkus.Proto;

namespace Phorkus.Consensus.HoneyBadger
{
    public class HoneyBadger : AbstractProtocol
    {
        // todo investigate where id should come from
        private HoneyBadgerId _honeyBadgerId;
        private ResultStatus _requested;
        private readonly bool[] _taken;

        private IRawShare? _rawShare;
        private EncryptedShare? _encryptedShare;
        private readonly IRawShare[] _shares;

        private readonly EncryptedShare[] _receivedShares;

        private ISet<IRawShare>? _result;

        private readonly ISet<PartiallyDecryptedShare>[] _decryptedShares;

        private PublicKey? PubKey => Wallet.TpkePublicKey;
        private PrivateKey? PrivKey => Wallet.TpkePrivateKey;
        private VerificationKey? VerificationKey => Wallet.TpkeVerificationKey;

        private bool _takenSet;
        private readonly ILogger<HoneyBadger> _logger = LoggerFactory.GetLoggerForClass<HoneyBadger>();


        public HoneyBadger(HoneyBadgerId honeyBadgerId, IWallet wallet, IConsensusBroadcaster broadcaster)
            : base(wallet, honeyBadgerId, broadcaster)
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
            _requested = ResultStatus.NotRequested;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Decrypted:
                        HandleDecryptedMessage(message.Validator, message.Decrypted);
                        break;
                    default:
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to {GetType().Name} protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                if (message is null) throw new ArgumentNullException();
                switch (message)
                {
                    case ProtocolRequest<HoneyBadgerId, IRawShare> honeyBadgerRequested:
                        HandleInputMessage(honeyBadgerRequested);
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> _:
                        Terminate();
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result:
                        HandleCommonSubset(result);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"protocol {GetType().Name} failed to handle internal message of type ${message.GetType()}");
                }
            }
        }

        private void HandleInputMessage(ProtocolRequest<HoneyBadgerId, IRawShare> request)
        {
            _rawShare = request.Input;
            _requested = ResultStatus.Requested;

            CheckEncryption();
            CheckResult();
        }

        private void CheckEncryption()
        {
            if (PubKey == null) return;
            if (_rawShare == null) return;
            if (_encryptedShare != null) return;
            _encryptedShare = PubKey.Encrypt(_rawShare);
            Broadcaster.InternalRequest(
                new ProtocolRequest<CommonSubsetId, EncryptedShare>(Id, new CommonSubsetId(_honeyBadgerId),
                    _encryptedShare));
        }

        private void CheckResult()
        {
            if (_result == null) return;
            if (_requested != ResultStatus.Requested) return;
            _logger.LogInformation($"Full result decrypted!");
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<HoneyBadgerId, ISet<IRawShare>>(_honeyBadgerId, _result));
        }

        private void HandleCommonSubset(ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result)
        {
            if (PrivKey is null) throw new InvalidOperationException();
            _logger.LogDebug($"Common subset finished {result.From}");
            foreach (EncryptedShare share in result.Result)
            {
                var dec = PrivKey.Decrypt(share);
                _taken[share.Id] = true;
                _receivedShares[share.Id] = share;
                // todo think about async access to protocol method. This may pose threat to protocol internal invariants
                CheckDecryptedShares(share.Id);

                Broadcaster.Broadcast(CreateDecryptedMessage(dec));
            }

            _takenSet = true;

            foreach (EncryptedShare share in result.Result)
            {
                CheckDecryptedShares(share.Id);
            }

            CheckResult();
        }

        private ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            if (PubKey is null) throw new InvalidOperationException();
            var message = new ConsensusMessage
            {
                Decrypted = PubKey.Encode(share)
            };
            return message;
        }

        private void HandleDecryptedMessage(Validator messageValidator, TPKEPartiallyDecryptedShareMessage msg)
        {
            if (PubKey is null || VerificationKey is null) throw new InvalidOperationException();
            PartiallyDecryptedShare share = PubKey.Decode(msg);
            if (_receivedShares[share.ShareId] != null)
                if (!VerificationKey.Verify(_receivedShares[share.ShareId], share))
                {
                    // possible fault evidence
                    return;
                }

            _decryptedShares[share.ShareId].Add(share);
            CheckDecryptedShares(share.ShareId);
        }

        private void CheckDecryptedShares(int id)
        {
            if (PubKey is null) throw new InvalidOperationException();
            if (!_takenSet) return;
            if (!_taken[id]) return;
            if (_decryptedShares[id].Count < F + 1) return;

            if (_shares[id] != null) return;

            _logger.LogInformation($"Collected {_decryptedShares[id].Count} shares for {id}, can decrypt now");
            _shares[id] = PubKey.FullDecrypt(_receivedShares[id], _decryptedShares[id].ToList());

            CheckAllSharesDecrypted();
        }

        private void CheckAllSharesDecrypted()
        {
            if (!_takenSet) return;
            if (_result != null) return;

            for (var i = 0; i < N; ++i)
                if (_taken[i] && _shares[i] == null)
                    return;

            _result = new HashSet<IRawShare>();

            for (var i = 0; i < N; ++i)
                if (_taken[i])
                {
                    _logger.LogDebug($"Added share {i} to result");
                    _result.Add(_shares[i]);
                }

            CheckResult();
        }

        private bool VerifyReceivedSharesByIndex(int i)
        {
            if (VerificationKey is null) throw new InvalidOperationException();
            if (_receivedShares[i] == null) return false;

            foreach (var part in _decryptedShares[i])
                if (!VerificationKey.Verify(_receivedShares[i], part))
                {
                    _logger.LogDebug($"{GetMyId()}: Verification failed {i}");
                    return false;
                }

            return true;
        }
    }
}