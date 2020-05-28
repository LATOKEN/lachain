using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lachain.Logger;
using Lachain.Consensus.CommonCoin;
using Lachain.Consensus.HoneyBadger;
using Lachain.Consensus.Messages;
using Lachain.Core.ValidatorStatus;
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
        private readonly ILogger<RootProtocol> _logger = LoggerFactory.GetLoggerForClass<RootProtocol>();
        private readonly EcdsaKeyPair _keyPair;
        private readonly RootProtocolId _rootId;
        private readonly IValidatorAttendanceRepository _validatorAttendanceRepository;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private IBlockProducer? _blockProducer;
        private ulong? _nonce;
        private UInt256[]? _hashes;
        private BlockHeader? _header;
        private MultiSig? _multiSig;

        private readonly List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>> _signatures =
            new List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>>();

        public RootProtocol(RootProtocolId id, IPublicConsensusKeySet wallet, ECDSAPrivateKey privateKey,
            IConsensusBroadcaster broadcaster, IValidatorAttendanceRepository validatorAttendanceRepository) : base(wallet, id, broadcaster)
        {
            _keyPair = new EcdsaKeyPair(privateKey);
            _rootId = id;
            _validatorAttendanceRepository = validatorAttendanceRepository;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage ?? throw new Exception("impossible");
                if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                {
                    throw new InvalidOperationException(
                        $"RootProtocol does not accept messages of type {message.PayloadCase}"
                    );
                }

                var signedHeaderMessage = message.SignedHeaderMessage;
                var idx = envelope.ValidatorIndex;
                _logger.LogDebug(
                    $"Received signature of header {signedHeaderMessage.Header.Keccak().ToHex()} " +
                    $"from validator {idx}: " +
                    $"pubKey {Wallet.EcdsaPublicKeySet[idx].EncodeCompressed().ToHex()}"
                );
                if (!(_header is null) && !_header.Equals(signedHeaderMessage.Header))
                {
                    _logger.LogWarning($"Received incorrect block header from validator {idx}");
                }

                if (!_crypto.VerifySignature(
                    signedHeaderMessage.Header.KeccakBytes(),
                    signedHeaderMessage.Signature.Encode(),
                    Wallet.EcdsaPublicKeySet[idx].EncodeCompressed()
                ))
                {
                    _logger.LogWarning(
                        $"Incorrect signature of header {signedHeaderMessage.Header.Keccak().ToHex()} from validator {idx}"
                    );
                }
                else
                {
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
                    validatorAttendance.IncrementAttendance(Wallet.EcdsaPublicKeySet[idx].EncodeCompressed(), message.SignedHeaderMessage.Header.Index);
                    _validatorAttendanceRepository.SaveState(validatorAttendance.ToBytes());
                }

                CheckSignatures();
            }
            else
            {
                var message = envelope.InternalMessage;
                switch (message)
                {
                    case ProtocolRequest<RootProtocolId, IBlockProducer> request:
                        _blockProducer = request.Input;
                        using (var stream = new MemoryStream())
                        {
                            foreach (var hash in _blockProducer.GetTransactionsToPropose(Id.Era).Select(tx => tx.Hash))
                            {
                                Debug.Assert(hash.ToBytes().Length == 32);
                                stream.Write(hash.ToBytes(), 0, 32);
                            }

                            var data = stream.ToArray();
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
                        _logger.LogDebug($"Received coin for block nonce: {_nonce}");
                        TrySignHeader();
                        CheckSignatures();
                        break;
                    case ProtocolResult<HoneyBadgerId, ISet<IRawShare>> result:
                        _logger.LogDebug($"Received shares {result.Result.Count} from HoneyBadger");

                        _hashes = result.Result.ToArray()
                            .SelectMany(share => SplitShare(share.ToBytes()))
                            .Select(hash => hash.ToUInt256())
                            .Distinct()
                            .ToArray();
                        _logger.LogDebug($"Collected {_hashes.Length} transactions in total");
                        TrySignHeader();
                        CheckSignatures();
                        break;
                    case ProtocolResult<RootProtocolId, object?> _:
                        Terminate();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(message));
                }
            }
        }

        private void TrySignHeader()
        {
            if (_hashes is null || _nonce is null || _blockProducer is null) return;
            if (!(_header is null)) return;
            try
            {
                _header = _blockProducer.CreateHeader((ulong) Id.Era, _hashes, _nonce.Value, out _hashes);
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot sign header because of {e}");
                Terminate();
                Environment.Exit(1);
                return;
            }

            var signature = _crypto.Sign(
                _header.KeccakBytes(),
                _keyPair.PrivateKey.Encode()
            ).ToSignature();
            _logger.LogDebug(
                $"Signed header {_header.Keccak().ToHex()} with pubKey {_keyPair.PublicKey.ToHex()}");
            Broadcaster.Broadcast(CreateSignedHeaderMessage(_header, signature));
        }

        private void CheckSignatures()
        {
            if (_header is null || _hashes is null || _blockProducer is null || _signatures.Count == 0) return;
            var bestHeader = _signatures
                .GroupBy(
                    tuple => tuple.Item1,
                    tuple => 1
                )
                .Select(p => new KeyValuePair<BlockHeader, int>(p.Key, p.Count()))
                .Aggregate((x, y) => x.Value > y.Value ? x : y);
            if (bestHeader.Value < Wallet.N - Wallet.F) return;
            _logger.LogDebug($"Received {bestHeader.Value} signatures for block header");
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
                    _logger.LogWarning($"Validator {signature.Key.ToHex()} signed wrong block header: {header}");
                }
            }

            try
            {
                _blockProducer.ProduceBlock(_hashes, _header, _multiSig);
            }
            catch (Exception e)
            {
                _logger.LogError($"Cannot produce block because of {e}");
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

        private static IEnumerable<byte[]> SplitShare(IReadOnlyCollection<byte> share)
        {
            for (var i = 0; i < share.Count; i += 32)
            {
                yield return share.Skip(i).Take(32).ToArray();
            }
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
            if (bytes is null || bytes.Length == 0) return new ValidatorAttendance(headerIndex / 1000);
            return ValidatorAttendance.FromBytes(bytes, headerIndex / 1000);
        }
    }
}