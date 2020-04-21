using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;

namespace Lachain.Consensus.HoneyBadger
{
    public class HoneyBadger : AbstractProtocol
    {
        private readonly HoneyBadgerId _honeyBadgerId;
        private ResultStatus _requested;
        private readonly ILogger<HoneyBadger> _logger = LoggerFactory.GetLoggerForClass<HoneyBadger>();

        private readonly PrivateKey _privateKey;

        private readonly EncryptedShare?[] _receivedShares;
        private IRawShare? _rawShare;
        private EncryptedShare? _encryptedShare;
        private readonly IRawShare?[] _shares;
        private readonly ISet<PartiallyDecryptedShare>[] _decryptedShares;
        private ISet<IRawShare>? _result;

        private readonly bool[] _taken;
        private bool _takenSet;

        public HoneyBadger(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet,
            PrivateKey privateKey, IConsensusBroadcaster broadcaster)
            : base(wallet, honeyBadgerId, broadcaster)
        {
            _honeyBadgerId = honeyBadgerId;
            _privateKey = privateKey;
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
                        HandleDecryptedMessage(message.Decrypted);
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
            if (_rawShare == null) return;
            if (_encryptedShare != null) return;
            _encryptedShare = Wallet.TpkePublicKey.Encrypt(_rawShare);
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
            _logger.LogDebug($"Common subset finished {result.From}");
            foreach (var share in result.Result)
            {
                var dec = _privateKey.Decrypt(share);
                _taken[share.Id] = true;
                _receivedShares[share.Id] = share;
                // todo think about async access to protocol method. This may pose threat to protocol internal invariants
                CheckDecryptedShares(share.Id);

                Broadcaster.Broadcast(CreateDecryptedMessage(dec));
            }

            _takenSet = true;

            foreach (var share in result.Result)
            {
                CheckDecryptedShares(share.Id);
            }

            CheckResult();
        }

        private ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = Wallet.TpkePublicKey.Encode(share)
            };
            return message;
        }

        private void HandleDecryptedMessage(TPKEPartiallyDecryptedShareMessage msg)
        {
            var share = Wallet.TpkePublicKey.Decode(msg);
            _decryptedShares[share.ShareId].Add(share);
            CheckDecryptedShares(share.ShareId);
        }

        private void CheckDecryptedShares(int id)
        {
            if (!_takenSet) return;
            if (!_taken[id]) return;
            if (_decryptedShares[id].Count < F + 1) return;

            if (_shares[id] != null) return;
            if (_receivedShares[id] is null) return;

            _logger.LogInformation($"Collected {_decryptedShares[id].Count} shares for {id}, can decrypt now");
            _shares[id] = Wallet.TpkePublicKey.FullDecrypt(_receivedShares[id], _decryptedShares[id].ToList());

            CheckAllSharesDecrypted();
        }

        private void CheckAllSharesDecrypted()
        {
            if (!_takenSet) return;
            if (_result != null) return;
            if (_taken.Zip(_shares, (b, share) => b && share is null).Any(x => x)) return;

            _result = _taken.Zip(_shares, (b, share) => (b, share))
                .Where(x => x.b)
                .Select(x => x.share ?? throw new Exception("impossible"))
                .ToHashSet();

            CheckResult();
        }
    }
}