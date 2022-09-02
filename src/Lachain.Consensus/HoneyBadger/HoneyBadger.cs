using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.TPKE;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.HoneyBadger
{
    public class HoneyBadger : AbstractProtocol
    {
        private static readonly ILogger<HoneyBadger> Logger = LoggerFactory.GetLoggerForClass<HoneyBadger>();

        private readonly HoneyBadgerId _honeyBadgerId;
        private readonly PrivateKey _privateKey;
        private readonly EncryptedShare?[] _receivedShares;
        private readonly IRawShare?[] _shares;
        private readonly ISet<PartiallyDecryptedShare>[] _decryptedShares;
        private readonly bool[] _taken;
        private ResultStatus _requested;
        private IRawShare? _rawShare;
        private EncryptedShare? _encryptedShare;
        private ISet<IRawShare>? _result;
        private bool _takenSet;
        private bool _skipDecryptedShareValidation;

        public HoneyBadger(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet,
            PrivateKey privateKey, bool skipDecryptedShareValidation, IConsensusBroadcaster broadcaster)
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
            _skipDecryptedShareValidation = skipDecryptedShareValidation;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode external message";
                    throw new ArgumentNullException();
                }
                switch (message.PayloadCase)
                {
                    case ConsensusMessage.PayloadOneofCase.Decrypted:
                        _lastMessage = "Decrypted";
                        HandleDecryptedMessage(message.Decrypted, envelope.ValidatorIndex);
                        break;
                    default:
                        _lastMessage = $"consensus message of type {message.PayloadCase} routed to {GetType().Name} protocol";
                        throw new ArgumentException(
                            $"consensus message of type {message.PayloadCase} routed to {GetType().Name} protocol"
                        );
                }
            }
            else
            {
                var message = envelope.InternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode internal message";
                    throw new ArgumentNullException();
                }
                switch (message)
                {
                    case ProtocolRequest<HoneyBadgerId, IRawShare> honeyBadgerRequested:
                        _lastMessage = "honeyBadgerRequested";
                        HandleInputMessage(honeyBadgerRequested);
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> _:
                        _lastMessage = "ProtocolResult";
                        Terminate();
                        break;
                    case ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result:
                        _lastMessage = "CommonSubset";
                        HandleCommonSubset(result);
                        break;
                    default:
                        _lastMessage =
                            $"protocol {GetType().Name} failed to handle internal message of type ${message.GetType()}";
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
            Logger.LogTrace($"Full result decrypted!");
            _requested = ResultStatus.Sent;
            Broadcaster.InternalResponse(
                new ProtocolResult<HoneyBadgerId, ISet<IRawShare>>(_honeyBadgerId, _result));
        }

        // TODO: (investigate) the share is comming from ReliableBroadcast and the shareId of the share should match the senderId
        // of that ReliableBroadcast, otherwise we might replace one share with another in _receivedShares array
        private void HandleCommonSubset(ProtocolResult<CommonSubsetId, ISet<EncryptedShare>> result)
        {
            Logger.LogTrace($"Common subset finished {result.From}");
            foreach (var share in result.Result)
            {
                var dec = _privateKey.Decrypt(share);
                _taken[share.Id] = true;
                _receivedShares[share.Id] = share;
                if (_decryptedShares[share.Id].Count > 0) // if we have any partially decrypted shares for this share - verify them
                {
                    if (!_skipDecryptedShareValidation)
                    {
                        if (Wallet.GetTpkeVerificationKey(share.Id) is null)
                            _decryptedShares[share.Id].Clear();
                        else
                            _decryptedShares[share.Id] = _decryptedShares[share.Id]
                                .Where(ps => Wallet.GetTpkeVerificationKey(ps.DecryptorId)!.VerifyShare(share, ps))
                                .ToHashSet();
                    }
                }

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

        protected virtual ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = Wallet.TpkePublicKey.Encode(share)
            };
            return message;
        }

        // DecryptorId of the message should match the senderId, otherwise the message should be discarded
        // because HoneyBadger does not accept two messages for same DecryptorId and shareId
        // We need to handle this message carefully like how about decoding a random message with random length
        // and the value of 'share.ShareId' needs to be checked. If it is out of range, it can throw exception
        private void HandleDecryptedMessage(TPKEPartiallyDecryptedShareMessage msg, int senderId)
        {
            PartiallyDecryptedShare? share = null;
            try
            {
                // Converting any random bytes to G1 is not possible
                share = Wallet.TpkePublicKey.Decode(msg);
                if (!(_receivedShares[share.ShareId] is null))
                {
                    if (!_skipDecryptedShareValidation)
                    {
                        if (Wallet.GetTpkeVerificationKey(share.DecryptorId) is null)
                            throw new Exception("No verification key for this sender");
                        if (!Wallet.GetTpkeVerificationKey(share.DecryptorId)!.VerifyShare(
                                _receivedShares[share.ShareId]!, share))
                            throw new Exception("Invalid share");
                    }
                }

                _decryptedShares[share.ShareId].Add(share);
            }
            catch (Exception ex)
            {
                share = null;
                var pubKey = Broadcaster.GetPublicKeyById(senderId)!.ToHex();
                Logger.LogWarning($"Exception occured handling Decrypted message: {msg} from {senderId} ({pubKey}), exception: {ex}");
            }

            if (!(share is null))
                CheckDecryptedShares(share.ShareId);
        }

        // There are several potential issues in Wallet.TpkePublicKey.FullDecrypt() that needs to be resolved.
        // It throws exception if more than one message is received from same decryptorId which can be easily exploited
        // Handling this exception is not enough, because we will not get the decrypted share. Another issue is anyone
        // can send a message with any value of decryptorId. It can stop or corrupt consensus.
        // Possible solution: try to validate decryptor id. Check if there is any relation between decryptor id and 
        // validator id. Discard any message in HandleDecryptedMessage() if the decryptor id does not match to
        // corresponding validator id (depends on the relation of decryptor id and validator id)
        private void CheckDecryptedShares(int id)
        {
            if (!_takenSet) return;
            if (!_taken[id]) return;
            if (_decryptedShares[id].Count < F + 1) return;
            if (_shares[id] != null) return;
            if (_receivedShares[id] is null) return;
            Logger.LogTrace($"Collected {_decryptedShares[id].Count} shares for {id}, can decrypt now");
            _shares[id] = Wallet.TpkePublicKey.FullDecrypt(_receivedShares[id]!, _decryptedShares[id].ToList());
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