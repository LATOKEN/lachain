using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.CommonSubset;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.ThresholdEncryption;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.HoneyBadger
{
    public class HoneyBadger : AbstractProtocol
    {
        private static readonly ILogger<HoneyBadger> Logger = LoggerFactory.GetLoggerForClass<HoneyBadger>();

        private readonly HoneyBadgerId _honeyBadgerId;
        private ResultStatus _requested;
        private IRawShare? _rawShare;
        private EncryptedShare? _encryptedShare;
        private ISet<IRawShare>? _result;
        private IThresholdEncryptor _thresholdEncryptor;

        public HoneyBadger(HoneyBadgerId honeyBadgerId, IPublicConsensusKeySet wallet,
            PrivateKeyShare privateKey, bool skipDecryptedShareValidation, IConsensusBroadcaster broadcaster)
            : base(wallet, honeyBadgerId, broadcaster)
        {
            _honeyBadgerId = honeyBadgerId;
            _requested = ResultStatus.NotRequested;
            _thresholdEncryptor = new ThresholdEncryptor(privateKey, wallet.ThresholdSignaturePublicKeySet, skipDecryptedShareValidation);
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
            _encryptedShare = _thresholdEncryptor.Encrypt(_rawShare);
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
            var encryptedShares = result.Result.ToList();
            var decryptedShares = _thresholdEncryptor.AddEncryptedShares(encryptedShares);
            foreach (var dec in decryptedShares)
            {

                // todo think about async access to protocol method. This may pose threat to protocol internal invariants
                CheckDecryptedShares(dec.ShareId);
                Broadcaster.Broadcast(CreateDecryptedMessage(dec));
            }

            foreach (var share in encryptedShares)
            {
                CheckDecryptedShares(share.Id);
            }

            CheckResult();
        }

        protected virtual ConsensusMessage CreateDecryptedMessage(PartiallyDecryptedShare share)
        {
            var message = new ConsensusMessage
            {
                Decrypted = share.Encode()
            };
            return message;
        }

        // DecryptorId of the message should match the senderId, otherwise the message should be discarded
        // because HoneyBadger does not accept two messages for same DecryptorId and shareId
        // We need to handle this message carefully like how about decoding a random message with random length
        // and the value of 'share.ShareId' needs to be checked. If it is out of range, it can throw exception
        private void HandleDecryptedMessage(TPKEPartiallyDecryptedShareMessage msg, int senderId)
        {
            bool added;
            try
            {
                added = _thresholdEncryptor.AddDecryptedShare(msg, senderId);
            }
            catch (Exception ex)
            {
                added = false;
                var pubKey = Broadcaster.GetPublicKeyById(senderId)!.ToHex();
                Logger.LogWarning($"Exception occured handling Decrypted message: {msg} from {senderId} ({pubKey}), exception: {ex}");
            }

            if (added)
                CheckDecryptedShares(msg.ShareId);
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
            var decrypted = _thresholdEncryptor.CheckDecryptedShares(id);
            if (decrypted)
                CheckAllSharesDecrypted();
        }

        private void CheckAllSharesDecrypted()
        {
            if (_result != null) return;

            if (!_thresholdEncryptor.GetResult(out var fullDecryptedShares))
            {
                return;
            }
            _result = fullDecryptedShares;

            CheckResult();
        }
    }
}