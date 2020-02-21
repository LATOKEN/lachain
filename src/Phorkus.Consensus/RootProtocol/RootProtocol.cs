using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Phorkus.Consensus.CommonCoin;
using Phorkus.Consensus.HoneyBadger;
using Phorkus.Consensus.Messages;
using Phorkus.Consensus.TPKE;
using Phorkus.Crypto;
using Phorkus.Logger;
using Phorkus.Proto;
using Phorkus.Utility.Utils;
using MessageEnvelope = Phorkus.Consensus.Messages.MessageEnvelope;

namespace Phorkus.Consensus.RootProtocol
{
    public class RootProtocol : AbstractProtocol
    {
        private readonly ILogger<RootProtocol> _logger = LoggerFactory.GetLoggerForClass<RootProtocol>();
        private readonly ECDSAKeyPair _keyPair;
        private readonly RootProtocolId _rootId;
        private readonly IWallet _wallet;
        private readonly ICrypto _crypto = CryptoProvider.GetCrypto();
        private IBlockProducer? _blockProducer;
        private ulong? _nonce;
        private UInt256[]? _hashes;
        private BlockHeader? _header;
        private MultiSig? _multiSig;

        private readonly List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>> _signatures =
            new List<Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>>();

        public RootProtocol(RootProtocolId id, IWallet wallet, IConsensusBroadcaster broadcaster)
            : base(wallet, id, broadcaster)
        {
            _keyPair = new ECDSAKeyPair(wallet.EcdsaPrivateKey, wallet.EcdsaPublicKey);
            _rootId = id;
            _wallet = wallet;
        }

        public override void ProcessMessage(MessageEnvelope envelope)
        {
            if (envelope.External)
            {
                var message = envelope.ExternalMessage;
                if (message.PayloadCase != ConsensusMessage.PayloadOneofCase.SignedHeaderMessage)
                {
                    throw new InvalidOperationException(
                        $"RootProtocol does not accept messages of type {message.PayloadCase}"
                    );
                }

                var signedHeaderMessage = message.SignedHeaderMessage;
                var idx = (int) message.Validator.ValidatorIndex;
                _logger.LogDebug(
                    $"Received signature of header {signedHeaderMessage.Header.Hash().ToHex()} " +
                    $"from validator {idx}: " +
                    $"pubKey {_wallet.EcdsaPublicKeySet[idx].EncodeCompressed().ToHex()}"
                );
                if (!(_header is null) && !_header.Equals(signedHeaderMessage.Header))
                {
                    _logger.LogWarning($"Received incorrect block header from validator {idx}");
                }

                if (!_crypto.VerifySignature(
                    signedHeaderMessage.Header.HashBytes(),
                    signedHeaderMessage.Signature.Encode(),
                    _wallet.EcdsaPublicKeySet[idx].EncodeCompressed()
                ))
                {
                    _logger.LogWarning(
                        $"Incorrect signature of header {signedHeaderMessage.Header.Hash().ToHex()} from validator {idx}"
                    );
                }
                else
                {
                    _signatures.Add(new Tuple<BlockHeader, MultiSig.Types.SignatureByValidator>(
                            signedHeaderMessage.Header,
                            new MultiSig.Types.SignatureByValidator
                            {
                                Key = _wallet.EcdsaPublicKeySet[idx],
                                Value = signedHeaderMessage.Signature,
                            }
                        )
                    );
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
                            foreach (var hash in _blockProducer.GetTransactionsToPropose().Select(tx => tx.Hash))
                            {
                                Debug.Assert(hash.ToBytes().Length == 32);
                                stream.Write(hash.ToBytes(), 0, 32);
                            }

                            var data = stream.GetBuffer();
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
                _header = _blockProducer.CreateHeader((ulong) Id.Era, _hashes, _keyPair.PublicKey, _nonce.Value);
            }
            catch (InvalidOperationException e)
            {
                _logger.LogError($"Cannot sign header because of {e}");
                Terminate();
                return;
            }

            var signature = _crypto.Sign(
                _header.HashBytes(),
                _keyPair.PrivateKey.Encode()
            ).ToSignature();
            _logger.LogDebug(
                $"Signed header {_header.Hash().ToHex()} with pubKey {_keyPair.PublicKey.ToHex()}");
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
            if (bestHeader.Value < _wallet.N - _wallet.F) return;
            _logger.LogDebug($"Received {bestHeader.Value} signatures for block header");
            _multiSig = new MultiSig {Quorum = (uint) (_wallet.N - _wallet.F)};
            _multiSig.Validators.AddRange(_wallet.EcdsaPublicKeySet);
            foreach (var (header, signature) in _signatures)
            {
                if (header.Equals(bestHeader.Key))
                {
                    _multiSig.Signatures.Add(signature);
                }
                else
                {
                    _logger.LogWarning($"Validator {signature.Key.Buffer.ToHex()} signed wrong block header: {header}");
                }
            }

            try
            {
                _blockProducer.ProduceBlock(_hashes, _header, _multiSig);
            }
            catch (InvalidOperationException e)
            {
                _logger.LogError(e, "Cannot produce block");
                Terminate();
                return;
            }

            Broadcaster.InternalResponse(new ProtocolResult<RootProtocolId, object?>(_rootId, null));
        }

        private ulong GetNonceFromCoin(CoinResult result)
        {
            var res = new byte[8];
            for (var i = 0; i < result.RawBytes.Length; ++i)
                res[i % 8] ^= result.RawBytes[i];
            return BitConverter.ToUInt64(res, 0);
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
                Validator = new Validator
                {
                    ValidatorIndex = GetMyId(),
                    Era = Id.Era
                },
                SignedHeaderMessage = new SignedHeaderMessage
                {
                    Header = header,
                    Signature = signature,
                }
            };
            return new ConsensusMessage(message);
        }
    }
}