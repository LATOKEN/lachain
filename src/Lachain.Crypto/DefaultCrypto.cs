using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Lachain.Proto;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Secp256k1Net;

namespace Lachain.Crypto
{
    public class DefaultCrypto : ICrypto
    {
        private static readonly X9ECParameters Curve = SecNamedCurves.GetByName("secp256k1");

        private static readonly ECDomainParameters Domain
            = new ECDomainParameters(Curve.Curve, Curve.G, Curve.N, Curve.H, Curve.GetSeed());

        private static readonly Secp256k1 Secp256K1 = new Secp256k1();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey)
        {
            var messageHash = message.KeccakBytes();
            return VerifySignatureHashed(messageHash, signature, publicKey);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifySignatureHashed(byte[] messageHash, byte[] signature, byte[] publicKey)
        {
            var pk = new byte[64];
            if (!Secp256K1.PublicKeyParse(pk, publicKey))
                throw new ArgumentException();


            var pubkeyDeserialized = new byte[33];
            if (!Secp256K1.PublicKeySerialize(pubkeyDeserialized, pk, Flags.SECP256K1_EC_COMPRESSED))
                throw new ArgumentException();

            var parsedSig = new byte[65];
            var recId = (signature[64] - 36) / 2 / TransactionUtils.ChainId;
            if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Take(64).ToArray(), recId))
                throw new ArgumentException();


            return Secp256K1.Verify(parsedSig.Take(64).ToArray(), messageHash, pk);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] Sign(byte[] message, byte[] privateKey)
        {
            var messageHash = message.KeccakBytes();
            return SignHashed(messageHash, privateKey);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] SignHashed(byte[] messageHash, byte[] privateKey)
        {
            var sig = new byte[65];
            if (!Secp256K1.SignRecoverable(sig, messageHash, privateKey))
                throw new ArgumentException();
            var serialized = new byte[64];
            if (!Secp256K1.RecoverableSignatureSerializeCompact(serialized, out var recId, sig))
                throw new ArgumentException();
            recId = TransactionUtils.ChainId * 2 + 35 + recId;
            return serialized.Concat(new[] {(byte) recId}).ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] RecoverSignature(byte[] message, byte[] signature)
        {
            var messageHash = message.KeccakBytes();
            return RecoverSignatureHashed(messageHash, signature);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] RecoverSignatureHashed(byte[] messageHash, byte[] signature)
        {
            var parsedSig = new byte[65];
            var pk = new byte[64];
            var recId = (signature[64] - 36) / 2 / TransactionUtils.ChainId;
            if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Take(64).ToArray(), recId))
                throw new ArgumentException();
            if (!Secp256K1.Recover(pk, parsedSig, messageHash))
                throw new ArgumentException("Bad signature");

            var result = new byte[33];
            if (!Secp256K1.PublicKeySerialize(result, pk, Flags.SECP256K1_EC_COMPRESSED))
            {
                throw new ArgumentException("Bad signature");
            }

            return result;
        }

        public byte[] ComputeAddress(byte[] publicKey)
        {
            return DecodePublicKey(publicKey, false).Skip(1).KeccakBytes().Skip(12).ToArray();
        }

        public byte[] ComputePublicKey(byte[] privateKey, bool compress = true)
        {
            if (privateKey == null)
                throw new ArgumentException(nameof(privateKey));

            var q = Domain.G.Multiply(new BigInteger(1, privateKey));
            var publicParams = new ECPublicKeyParameters(q, Domain);

            var result = publicParams.Q.GetEncoded(compress);
            return result;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] DecodePublicKey(byte[] publicKey, bool compress)
        {
            var pubKeyParsed = new byte[64];
            if (!Secp256K1.PublicKeyParse(pubKeyParsed, publicKey))
                throw new ArgumentException(nameof(publicKey));
            var result = compress ? new byte[33] : new byte[65];
            if (!Secp256K1.PublicKeySerialize(result, pubKeyParsed,
                compress ? Flags.SECP256K1_EC_COMPRESSED : Flags.SECP256K1_EC_UNCOMPRESSED)
            )
                throw new ArgumentException(nameof(publicKey));
            return result;
        }

        public byte[] GenerateRandomBytes(int length)
        {
            if (length < 1)
                throw new ArgumentException(nameof(length));

            var privateKey = new byte[length];
            using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                rng.GetBytes(privateKey);

            return privateKey;
        }
    }
}