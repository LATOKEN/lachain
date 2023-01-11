using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Consensus.ThresholdKeygen;
using Lachain.Consensus.ThresholdKeygen.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Lachain.UtilityTest;
using MCL.BLS12_381.Net;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;

namespace Lachain.ConsensusTest
{
    public class MaliciousKeygen : TrustlessKeygen
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

        public MaliciousKeygen(EcdsaKeyPair keyPair, IEnumerable<ECDSAPublicKey> publicKeys, int f, ulong cycle)
            : base(keyPair, publicKeys, f, cycle)
        {
            _keyPair = keyPair;
            _publicKeys = publicKeys.ToArray();
            _myIdx = _publicKeys.FirstIndexOf(keyPair.PublicKey);
            _keyGenStates = Enumerable.Range(0, Players).Select(_ => new State(Players)).ToArray();
            _confirmSent = false;
        }

        protected MaliciousKeygen(EcdsaKeyPair keyPair, IEnumerable<ECDSAPublicKey> publicKeys, int f, ulong cycle,
            State[] states, IList<int> finished, IDictionary<UInt256, int> confirmations, bool confirmSent)
            : base(keyPair, publicKeys, f, cycle, states, finished, confirmations, confirmSent)
        {
            _keyPair = keyPair;
            _publicKeys = publicKeys.ToArray();
            _myIdx = _publicKeys.FirstIndexOf(keyPair.PublicKey);
            _keyGenStates = states;
            _finished = finished;
            _confirmations = confirmations;
            _confirmSent = confirmSent;
        }

        public override ValueMessage HandleCommit(int sender, CommitMessage message)
        {
            if (message.EncryptedRows.Length != Players) throw new ArgumentException();
            if (_keyGenStates[sender].Commitment != null)
                throw new ArgumentException($"Double commit from sender {sender}");
            _keyGenStates[sender].Commitment = message.Commitment;
            var myRowCommitted = message.Commitment.Evaluate(_myIdx + 1);
            var myRow = DecryptRow(message.EncryptedRows[_myIdx], _keyPair.PrivateKey).ToArray();
            if (!myRow.Select(x => G1.Generator * x).SequenceEqual(myRowCommitted))
                throw new ArgumentException("Commitment does not match");

            return new ValueMessage
            {
                Proposer = sender,
                EncryptedValues = Enumerable.Range(0, Players).Select(i => UtilityTest.TestUtils.GetRandomBytes()).ToArray()
            };
        }

        public static MaliciousKeygen FromBytes(ReadOnlyMemory<byte> bytes,  EcdsaKeyPair keyPair)
        {
            var players = bytes.Slice(0, 4).Span.ToInt32();
            var faulty = bytes.Slice(4, 4).Span.ToInt32();
            var cycle = bytes.Slice(8, 8).Span.ToUInt64();
            var ecdsaPublicKeys = bytes.Slice(16, CryptoUtils.PublicKeyLength * players)
                .Batch(CryptoUtils.PublicKeyLength)
                .Select(x => x.ToPublicKey())
                .ToArray();
            var offset = 16 + CryptoUtils.PublicKeyLength * players;
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
            return new MaliciousKeygen(keyPair, ecdsaPublicKeys, faulty, cycle, states, finished, confirmations, confirmSent);
        }
    }
}