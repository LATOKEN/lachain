using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Consensus.TPKE.Data;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Logger;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Consensus.TPKE
{
    public class TrustlessKeygen
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();
        private static readonly ILogger<TrustlessKeygen> Logger = LoggerFactory.GetLoggerForClass<TrustlessKeygen>();

        private readonly EcdsaKeyPair _keyPair;
        private readonly ECDSAPublicKey[] _publicKeys;
        private readonly int _n;
        private readonly int _f;
        private readonly int _myIdx;

        private readonly State[] _keyGenStates;

        public TrustlessKeygen(EcdsaKeyPair keyPair, IEnumerable<ECDSAPublicKey> publicKeys, int f)
        {
            _keyPair = keyPair;
            _publicKeys = publicKeys.ToArray();
            _n = _publicKeys.Length;
            _f = f;
            _myIdx = _publicKeys.FirstIndexOf(keyPair.PublicKey);
            _keyGenStates = new State[_n];
        }

        public CommitMessage StartKeygen()
        {
            var biVarPoly = BiVarSymmetricPolynomial.Random(_f);
            var commitment = biVarPoly.Commit();

            var rows = Enumerable.Range(1, _n)
                .Select(i => biVarPoly.Evaluate(i))
                .Select((row, i) => EncryptRow(row, _publicKeys[i]))
                .ToArray();

            return new CommitMessage
            {
                Commitment = commitment,
                EncryptedRows = rows
            };
        }

        public ValueMessage HandleCommit(int sender, CommitMessage message)
        {
            if (message.EncryptedRows.Length != _n) throw new ArgumentException();
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
                EncryptedValues = Enumerable.Range(0, _n).Select(i => Crypto.Secp256K1Encrypt(
                    _publicKeys[i].EncodeCompressed(),
                    Fr.ToBytes(Mcl.GetValue(myRow, i + 1))
                )).ToArray()
            };
        }

        public void HandleSendValue(int sender, ValueMessage message)
        {
            if (_keyGenStates[message.Proposer].Acks[sender])
                throw new ArgumentException("Already handled this value");
            var myValue = Fr.FromBytes(Crypto.Secp256K1Decrypt(
                _keyPair.PrivateKey.Encode(), message.EncryptedValues[_myIdx]
            ));
            if (!_keyGenStates[message.Proposer].Commitment.Evaluate(sender + 1, _myIdx + 1)
                .Equals(G1.Generator * myValue)
            )
                throw new ArgumentException("Decrypted value does not match commitment");
            _keyGenStates[message.Proposer].Values[sender] = myValue;
        }

        private static byte[] EncryptRow(IEnumerable<Fr> row, ECDSAPublicKey publicKey)
        {
            var serializedRow = row.Select(Fr.ToBytes)
                .Cast<IEnumerable<byte>>()
                .Aggregate((a, b) => a.Concat(b))
                .ToArray();
            return Crypto.Secp256K1Encrypt(publicKey.EncodeCompressed(), serializedRow);
        }

        private static IEnumerable<Fr> DecryptRow(byte[] encryptedRow, ECDSAPrivateKey privateKey)
        {
            return Crypto.Secp256K1Decrypt(privateKey.Encode(), encryptedRow)
                .Batch(Fr.ByteSize)
                .Select(Fr.FromBytes);
        }
    }
}