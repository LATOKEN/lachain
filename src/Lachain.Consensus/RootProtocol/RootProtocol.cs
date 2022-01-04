using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Storage.Repositories;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MessageEnvelope = Lachain.Consensus.Messages.MessageEnvelope;

namespace Lachain.Consensus.RootProtocol
{
    public class RootProtocol : AbstractProtocol
    {
        private static readonly ILogger<RootProtocol> Logger = LoggerFactory.GetLoggerForClass<RootProtocol>();
        private readonly EcdsaKeyPair _keyPair;
        private readonly RootProtocolId _rootId;
        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private IBlockProducer? _blockProducer;
        private ulong? _nonce;
        private TransactionReceipt[]? _receipts;
        private BlockHeader? _header;
        private MultiSig? _multiSig;
        private ulong _cycleDuration;

        private readonly List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>> _signatures =
            new List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>>();

        public RootProtocol(RootProtocolId id, IPublicConsensusKeySet wallet, ECDSAPrivateKey privateKey,
            IConsensusBroadcaster broadcaster, IValidatorAttendanceRepository validatorAttendanceRepository,
            ulong cycleDuration) : base(wallet, id, broadcaster)
        {
            _keyPair = new EcdsaKeyPair(privateKey);
            _rootId = id;
            _validatorAttendanceRepository = validatorAttendanceRepository;
            _cycleDuration = cycleDuration;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                Logger.LogTrace("External envelope");
                var message = envelope.ExternalMessage;
                if (message is null)
                {
                    _lastMessage = "Failed to decode external message";
                    throw new Exception("impossible");
                }
                if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                {
                    _lastMessage = $"RootProtocol does not accept messages of type {message.PayloadCase}";
                    throw new InvalidOperationException(
                        $"RootProtocol does not accept messages of type {message.PayloadCase}"
                    );
                }

                _lastMessage = "SignedHeaderMessage";
                var signedHeaderMessage = message.SignedHeaderMessage;
                var idx = envelope.ValidatorIndex;
                Logger.LogTrace(
                    $"Received signature of header {signedHeaderMessage.Header.Keccak().ToHex()} " +
                    $"from validator {idx}: " +
                    $"pubKey {Wallet.EcdsaPublicKeySet[idx].EncodeCompressed().ToHex()}"
                );
                if (!(_header is null) && !_header.Equals(signedHeaderMessage.Header))
                {
                    Logger.LogWarning($"Received incorrect block header from validator {idx}");
                    Logger.LogWarning($"Header we have {_header}");
                    Logger.LogWarning($"Header we received {signedHeaderMessage.Header}");
                }

                if (!_crypto.VerifySignatureHashed(
                    signedHeaderMessage.Header.Keccak().ToBytes(),
                    signedHeaderMessage.Signature.Encode(),
                    Wallet.EcdsaPublicKeySet[idx].EncodeCompressed()
                ))
                {
                    _lastMessage =
                        $"Incorrect signature of header {signedHeaderMessage.Header.Keccak().ToHex()} from validator {idx}";
                    Logger.LogWarning(
                        $"Incorrect signature of header {signedHeaderMessage.Header.Keccak().ToHex()} from validator {idx}"
                    );
                }
                else
                {
                    Logger.LogTrace("Add signatures");
                    _lastMessage = "Add signatures";
                    _signatures.Add(new Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>(
                            signedHeaderMessage.Header,
                            new MultiSig.Types.SignatureByValidator
                            {
                                Key = Wallet.EcdsaPublicKeySet[idx],
                                Value = signedHeaderMessage.Signature,
                            }
                        )
                    );
                    var validatorAttendance = GetOrCreateValidatorAttendance(message.SignedHeaderMessage.Header.Index);
                    validatorAttendance!.IncrementAttendanceForCycle(Wallet.EcdsaPublicKeySet[idx].EncodeCompressed(),
                        message.SignedHeaderMessage.Header.Index / _cycleDuration);
                    _validatorAttendanceRepository.SaveState(validatorAttendance.ToBytes());
                }

                CheckSignatures();
            }
            else
            {
                Logger.LogTrace("Internal envelop");
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<RootProtocolId, IBlockProducer> request:
                        Logger.LogTrace("request");
                        _lastMessage = "ProtocolRequest";
                        _blockProducer = request.Input;
                        using (var stream = new MemoryStream())
                        {
                            var proposed = _blockProducer.GetTransactionsToPropose(Id.Era).ToList();
                            while(proposed.Count() > 1 && proposed.Count() < 500) 
                            {
                                var tx = proposed.Last();
                                proposed.Add(tx);
                            }
                            var data = _blockProducer.GetTransactionsToPropose(Id.Era).ToByteArray();
                            Broadcaster.InternalRequest(new ProtocolRequest<HoneyBadgerId, IRawShare>(
                                Id, new HoneyBadgerId(Id.Era), new RawShare(data, GetMyId()))
                            );
                        }

                        Broadcaster.InternalRequest(new ProtocolRequest<CoinId, object?>(
                            Id, new CoinId(Id.Era, -1, 0), null
                        ));
                        TrySignHeader();
                        CheckSignatures();
                        break;
                    case ProtocolResult<CoinId, CoinResult> coinResult:
                        _nonce = GetNonceFromCoin(coinResult.Result);
                        Logger.LogTrace($"Received coin for block nonce: {_nonce}");
                        _lastMessage = $"Received coin for block nonce: {_nonce}";
                        TrySignHeader();
                        CheckSignatures();
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> result:
                        Logger.LogTrace($"Received shares {result.Result.Count} from HoneyBadger");
                        _lastMessage = $"Received shares {result.Result.Count} from HoneyBadger";

                        var rawShares = result.Result.ToArray();
                        var receipts = new List<TransactionReceipt>();

                        foreach(var rawShare in rawShares)
                        {
                            // this rawShare may be malicious
                            // we may need to discard this if any issue arises during decoding
                            try 
                            {
                                var contributions = rawShare.ToBytes().ToMessageArray<TransactionReceipt>();
                                foreach(var receipt in contributions)
                                    receipts.Add(receipt);
                            }
                            catch(Exception e)
                            {
                                Logger.LogError($"Skipped a rawShare due to exception: {e.Message}");
                                Logger.LogError($"One of the validators might be malicious!!!");
                            }
                        }
                        Logger.LogTrace($"Collected {receipts.Count()} non-distinct transactions in total");
                        _receipts = receipts.Distinct().ToArray();
                        Logger.LogTrace($"Collected {_receipts.Length} transactions in total");
                        TrySignHeader();
                        CheckSignatures();
                        break;
                    case ProtocolResult<RootProtocolId, object?> _:
                        Logger.LogTrace("Terminate in switch");
                        _lastMessage = "Terminate in switch";
                        Terminate();
                        break;
                    default:
                        _lastMessage = "Invalid message";
                        Logger.LogError("Invalid message");
                        throw new ArgumentOutOfRangeException(nameof(message));
                }
            }
        }

        private void TrySignHeader()
        {
            Logger.LogTrace("TrySignHeader");
            if (_receipts is null || _nonce is null || _blockProducer is null)
            {
                Logger.LogTrace($"Not ready yet: _hasges {_receipts is null}, _nonce {_nonce is null}, _blockProducer {_blockProducer is null}");
                return;
            }

            if (!(_header is null))
            {
                Logger.LogTrace("Header already exists");
                return;
            }
            try
            {
                Logger.LogTrace("Try to create header");
                _header = _blockProducer.CreateHeader((ulong) Id.Era, _receipts, _nonce.Value, out _receipts);
                if (_header == null)
                {
                    Logger.LogTrace("Failed to create header");
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"Cannot sign header because of {e}");
                Terminate();
                Environment.Exit(1);
                return;
            }

            var signature = _crypto.SignHashed(
                _header.Keccak().ToBytes(),
                _keyPair.PrivateKey.Encode()
            ).ToSignature();
            Logger.LogTrace(
                $"Signed header {_header.Keccak().ToHex()} with pubKey {_keyPair.PublicKey.ToHex()}"
            );
            Broadcaster.Broadcast(CreateSignedHeaderMessage(_header, signature));
        }

        private void CheckSignatures()
        {
            Logger.LogTrace("CheckSignatures");
            if (_header is null || _receipts is null || _blockProducer is null || _signatures.Count == 0)
            {
                Logger.LogTrace($"Not ready yet: _header {_header is null}, _hashes {_receipts is null}, _blockProducer {_blockProducer is null}, _signatures {_signatures.Count}");
                return;
            }
            var bestHeader = _signatures
                .GroupBy(
                    tuple => tuple.Item1,
                    tuple => 1
                )
                .Select(p => new KeyValuePair<BlockHeader, int>(p.Key, p.Count()))
                .Aggregate((x, y) => x.Value > y.Value ? x : y);
            Logger.LogTrace($"bestHeader.Value {bestHeader.Value} Wallet.N {Wallet.N}  Wallet.F {Wallet.F}");
            if (bestHeader.Value < Wallet.N - Wallet.F) return;
            Logger.LogTrace($"Received {bestHeader.Value} signatures for block header");
            _multiSig = new MultiSig {Quorum = (uint) (Wallet.N - Wallet.F)};
            _multiSig.Validators.AddRange(Wallet.EcdsaPublicKeySet);
            foreach (var (header, signature) in _signatures)
            {
                if (header.Equals(bestHeader.Key))
                {
                    _multiSig.Signatures.Add(signature);
                }
                else
                {
                    Logger.LogWarning($"Validator {signature.Key.ToHex()} signed wrong block header: {header}");
                }
            }

            try
            {
                _blockProducer.ProduceBlock(_receipts, _header, _multiSig);
            }
            catch (Exception e)
            {
                Logger.LogError($"Cannot produce block because of {e}");
                Terminate();
                Environment.Exit(1);
                return;
            }

            Broadcaster.InternalResponse(new ProtocolResult<RootProtocolId, object?>(_rootId, null));
        }

        private ulong GetNonceFromCoin(CoinResult result)
        {
            var res = new byte[8];
            for (var i = 0; i < result.RawBytes.Length; ++i)
                res[i % 8] ^= result.RawBytes[i];
            return res.AsReadOnlySpan().ToUInt64();
        }
        private ConsensusMessage CreateSignedHeaderMessage(BlockHeader header, Signature signature)
        {
            var message = new ConsensusMessage
            {
                SignedHeaderMessage = new SignedHeaderMessage
                {
                    Header = header,
                    Signature = signature,
                }
            };
            return new ConsensusMessage(message);
        }

        private ValidatorAttendance? GetOrCreateValidatorAttendance(ulong headerIndex)
        {
            var bytes = _validatorAttendanceRepository.LoadState();
            if (bytes is null || bytes.Length == 0) return new ValidatorAttendance(headerIndex / _cycleDuration);
            return ValidatorAttendance.FromBytes(bytes, headerIndex / _cycleDuration,
                headerIndex % _cycleDuration >= _cycleDuration / 10);
        }
    }
}