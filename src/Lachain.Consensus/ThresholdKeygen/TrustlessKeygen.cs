using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using MCL.BLS12_381.Net;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.Consensus.ThresholdKeygen
{
    public class TrustlessKeygen : IEquatable<TrustlessKeygen>
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ILogger<TrustlessKeygen> Logger = LoggerFactory.GetLoggerForClass<TrustlessKeygen>();

        private readonly EcdsaKeyPair _keyPair;
        private readonly ECDSAPublicKey[] _publicKeys;
        private readonly int _myIdx;
        private readonly State[] _keyGenStates;
        private readonly IList<int> _finished = new List<int>();
        private readonly IDictionary<UInt256, int> _confirmations = new Dictionary<UInt256, int>();
        private bool _confirmSent;

        public int Faulty { get; }
        public int Players { get; }

        public TrustlessKeygen(EcdsaKeyPair keyPair, IEnumerable<ECDSAPublicKey> publicKeys, int f)
        {
            _keyPair = keyPair;
            _publicKeys = publicKeys.ToArray();
            Players = _publicKeys.Length;
            Faulty = f;
            _myIdx = _publicKeys.FirstIndexOf(keyPair.PublicKey);
            _keyGenStates = Enumerable.Range(0, Players).Select(_ => new State(Players)).ToArray();
            _confirmSent = false;
        }

        private TrustlessKeygen(EcdsaKeyPair keyPair, IEnumerable<ECDSAPublicKey> publicKeys, int f,
            State[] states, IList<int> finished, IDictionary<UInt256, int> confirmations, bool confirmSent)
        {
            _keyPair = keyPair;
            _publicKeys = publicKeys.ToArray();
            Players = _publicKeys.Length;
            Faulty = f;
            _myIdx = _publicKeys.FirstIndexOf(keyPair.PublicKey);
            _keyGenStates = states;
            _finished = finished;
            _confirmations = confirmations;
            _confirmSent = confirmSent;
        }

        public CommitMessage StartKeygen()
        {
            var biVarPoly = BiVarSymmetricPolynomial.Random(Faulty);
            var commitment = biVarPoly.Commit();
            var rows = Enumerable.Range(1, Players)
                .Select(i => biVarPoly.Evaluate(i))
                .Select((row, i) => EncryptRow(row, _publicKeys[i]))
                .ToArray();
            return new CommitMessage
            {
                Commitment = commitment,
                EncryptedRows = rows
            };
        }

        public int GetSenderByPublicKey(ECDSAPublicKey publicKey)
        {
            try
            {
                return _publicKeys.FirstIndexOf(publicKey);
            }
            catch (Exception)
            {
                return -1;
            }
        }

        public ValueMessage HandleCommit(int sender, CommitMessage message)
        {
            if (message.EncryptedRows.Length != Players) throw new ArgumentException();
            if ((_keyGenStates[sender].Commitment != null) && (_keyGenStates[sender].Commitment != message.Commitment))
                throw new ArgumentException($"Double commit from sender {sender}");
            _keyGenStates[sender].Commitment = message.Commitment;
            var myRowCommitted = message.Commitment.Evaluate(_myIdx + 1);
            var myRow = DecryptRow(message.EncryptedRows[_myIdx], _keyPair.PrivateKey).ToArray();
            if (!myRow.Select(x => G1.Generator * x).SequenceEqual(myRowCommitted))
                throw new ArgumentException("Commitment does not match");

            return new ValueMessage
            {
                Proposer = sender,
                EncryptedValues = Enumerable.Range(0, Players).Select(i => Crypto.Secp256K1Encrypt(
                    _publicKeys[i].EncodeCompressed(),
                    MclBls12381.EvaluatePolynomial(myRow, Fr.FromInt(i + 1)).ToBytes()
                )).ToArray()
            };
        }

        public bool HandleSendValue(int sender, ValueMessage message)
        {
            if (_keyGenStates[message.Proposer].Acks[sender])
                throw new ArgumentException("Already handled this value");
            _keyGenStates[message.Proposer].Acks[sender] = true;
            var myValue = Fr.FromBytes(Crypto.Secp256K1Decrypt(
                _keyPair.PrivateKey.Encode(), message.EncryptedValues[_myIdx]
            ));
            if (_keyGenStates[message.Proposer].Commitment is null)
                throw new ArgumentException("Cannot handle value since there was no commitment yet");
            if (!_keyGenStates[message.Proposer].Commitment!.Evaluate(_myIdx + 1, sender + 1)
                .Equals(G1.Generator * myValue)
            )
                throw new ArgumentException("Decrypted value does not match commitment");
            _keyGenStates[message.Proposer].Values[sender] = myValue;
            if (_keyGenStates[message.Proposer].ValueCount() > 2 * Faulty && !_finished.Contains(message.Proposer))
            {
                _finished.Add(message.Proposer);
            }

            if (_confirmSent) return false;
            if (!Finished()) return false;
            _confirmSent = true;
            return true;
        }

        public bool HandleConfirm(PublicKey tpkeKey, PublicKeySet tsKeys)
        {
            var keyringHash = tpkeKey.ToBytes().Concat(tsKeys.ToBytes()).Keccak();
            _confirmations.PutIfAbsent(keyringHash, 0);
            _confirmations[keyringHash] += 1;
            return _confirmations[keyringHash] == Players - Faulty;
        }

        public bool Finished()
        {
            return _keyGenStates.Count(s => s.ValueCount() > 2 * Faulty) > Faulty;
        }

        public ThresholdKeyring? TryGetKeys()
        {
            if (!Finished()) return null;
            var pubKeyPoly = Enumerable.Range(0, Faulty + 1)
                .Select(_ => G1.Zero)
                .ToArray();
            var secretKey = Fr.Zero;
            foreach (var dealer in _finished.Take(Faulty + 1))
            {
                var s = _keyGenStates[dealer];
                if (s.ValueCount() <= 2 * Faulty) throw new Exception("Impossible"); // just in case
                var rowZero = s.Commitment!.Evaluate(0).ToArray();
                foreach (var (x, i) in rowZero.WithIndex())
                    pubKeyPoly[i] += x;
                secretKey += s.InterpolateValues();
            }

            var pubKeys = Enumerable.Range(0, Players + 1)
                .Select(i => MclBls12381.EvaluatePolynomial(pubKeyPoly, Fr.FromInt(i)))
                .ToArray();

            return new ThresholdKeyring
            {
                TpkePrivateKey = new PrivateKey(secretKey, _myIdx),
                TpkePublicKey = new PublicKey(pubKeys[0], Faulty),
                ThresholdSignaturePrivateKey = new PrivateKeyShare(secretKey),
                ThresholdSignaturePublicKeySet =
                    new PublicKeySet(pubKeys.Skip(1).Select(x => new Crypto.ThresholdSignature.PublicKey(x)), Faulty)
            };
        }

        private static byte[] EncryptRow(IEnumerable<Fr> row, ECDSAPublicKey publicKey)
        {
            var serializedRow = row.Select(x => x.ToBytes()).Flatten().ToArray();
            return Crypto.Secp256K1Encrypt(publicKey.EncodeCompressed(), serializedRow);
        }

        private static IEnumerable<Fr> DecryptRow(byte[] encryptedRow, ECDSAPrivateKey privateKey)
        {
            return Crypto.Secp256K1Decrypt(privateKey.Encode(), encryptedRow)
                .Batch(Fr.ByteSize)
                .Select(b => Fr.FromBytes(b.ToArray()));
        }

        public byte[] ToBytes()
        {
            using var stream = new MemoryStream();
            stream.Write(Players.ToBytes().ToArray());
            stream.Write(Faulty.ToBytes().ToArray());
            foreach (var publicKey in _publicKeys)
                stream.Write(publicKey.Buffer.ToArray());
            foreach (var keyGenState in _keyGenStates)
            {
                var bytes = keyGenState.ToBytes();
                stream.Write(bytes.Length.ToBytes().ToArray());
                stream.Write(bytes);
            }

            stream.Write(_finished.Count.ToBytes().ToArray());
            foreach (var f in _finished)
                stream.Write(f.ToBytes().ToArray());
            stream.Write(_confirmations.Count.ToBytes().ToArray());
            foreach (var confirmation in _confirmations)
            {
                stream.Write(confirmation.Key.ToBytes());
                stream.Write(confirmation.Value.ToBytes().ToArray());
            }

            stream.Write(new[] {_confirmSent ? (byte) 1 : (byte) 0});
            return stream.ToArray();
        }

        public static TrustlessKeygen FromBytes(ReadOnlyMemory<byte> bytes, EcdsaKeyPair keyPair)
        {
            var players = bytes.Slice(0, 4).Span.ToInt32();
            var faulty = bytes.Slice(4, 4).Span.ToInt32();
            var ecdsaPublicKeys = bytes.Slice(8, CryptoUtils.PublicKeyLength * players)
                .Batch(CryptoUtils.PublicKeyLength)
                .Select(x => x.ToPublicKey())
                .ToArray();
            var offset = 8 + CryptoUtils.PublicKeyLength * players;
            var states = new State[players];
            for (var i = 0; i < players; ++i)
            {
                var len = bytes.Slice(offset, 4).Span.ToInt32();
                offset += 4;
                states[i] = State.FromBytes(bytes.Slice(offset, len));
                offset += len;
            }

            var finishedCnt = bytes.Slice(offset, 4).Span.ToInt32();
            offset += 4;
            var finished = bytes.Slice(offset, finishedCnt * 4)
                .Batch(4)
                .Select(x => x.Span.ToInt32())
                .ToList();
            offset += 4 * finishedCnt;
            var confirmationCnt = bytes.Slice(offset, 4).Span.ToInt32();
            offset += 4;
            var confirmations = bytes.Slice(offset, (32 + 4) * confirmationCnt)
                .Batch(32 + 4)
                .Select(x => new KeyValuePair<UInt256, int>(
                    x.Slice(0, 32).ToArray().ToUInt256(),
                    x.Slice(32, 4).Span.ToInt32())
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
            offset += (32 + 4) * confirmationCnt;
            var confirmSent = bytes.Slice(offset).Span[0] != 0;
            return new TrustlessKeygen(keyPair, ecdsaPublicKeys, faulty, states, finished, confirmations, confirmSent);
        }

        public bool Equals(TrustlessKeygen? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return _keyPair.Equals(other._keyPair) &&
                   _publicKeys.SequenceEqual(other._publicKeys) &&
                   _myIdx == other._myIdx &&
                   _keyGenStates.SequenceEqual(other._keyGenStates) &&
                   _finished.SequenceEqual(other._finished) &&
                   _confirmations.SequenceEqual(other._confirmations) &&
                   _confirmSent == other._confirmSent &&
                   Faulty == other.Faulty &&
                   Players == other.Players;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((TrustlessKeygen) obj);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Faulty);
            hashCode.Add(Players);
            hashCode.Add(_keyPair.PrivateKey);
            return hashCode.ToHashCode();
        }
    }
}