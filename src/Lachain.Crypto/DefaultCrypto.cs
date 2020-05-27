using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
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

            var publicKeySerialized = new byte[33];
            if (!Secp256K1.PublicKeySerialize(publicKeySerialized, pk, Flags.SECP256K1_EC_COMPRESSED))
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
            var publicKey = new byte[64];
            var result = new byte[33];
            if (!Secp256K1.PublicKeyCreate(publicKey, privateKey))
                throw new ArgumentException("Bad private key");
            if (!Secp256K1.PublicKeySerialize(result, publicKey, Flags.SECP256K1_EC_COMPRESSED))
                throw new ArgumentException("Bad public key");
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

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TryDecodePublicKey(byte[] publicKey, bool compress, out byte[] normalizedKey)
        {
            var pubKeyParsed = new byte[64];
            if (!Secp256K1.PublicKeyParse(pubKeyParsed, publicKey))
            {
                normalizedKey = Array.Empty<byte>();
                return false;
            }

            normalizedKey = compress ? new byte[33] : new byte[65];
            if (Secp256K1.PublicKeySerialize(normalizedKey, pubKeyParsed,
                compress ? Flags.SECP256K1_EC_COMPRESSED : Flags.SECP256K1_EC_UNCOMPRESSED)) return true;
            normalizedKey = Array.Empty<byte>();
            return false;
        }

        public byte[] GenerateRandomBytes(int length)
        {
            if (length < 1)
                throw new ArgumentException(nameof(length));

            var privateKey = new byte[length];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(privateKey);

            return privateKey;
        }

        public byte[] GeneratePrivateKey()
        {
            for (;;)
            {
                var key = GenerateRandomBytes(32);
                if (Secp256K1.SecretKeyVerify(key)) return key;
            }
        }

        public byte[] AesGcmEncrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> plaintext)
        {
            var tag = new byte[AesGcm.TagByteSizes.MaxSize];
            var nonce = GenerateRandomBytes(AesGcm.NonceByteSizes.MaxSize);
            var ciphertext = new byte[plaintext.Length];
            using var aes = new AesGcm(key);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
            return tag.Concat(nonce).Concat(ciphertext).ToArray();
        }

        public byte[] AesGcmDecrypt(ReadOnlySpan<byte> key, ReadOnlySpan<byte> ciphertext)
        {
            if (ciphertext.Length < AesGcm.NonceByteSizes.MaxSize + AesGcm.TagByteSizes.MaxSize)
                throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));
            var tag = ciphertext.Slice(0, AesGcm.TagByteSizes.MaxSize);
            var nonce = ciphertext.Slice(AesGcm.TagByteSizes.MaxSize, AesGcm.NonceByteSizes.MaxSize);
            var result = new byte[ciphertext.Length - AesGcm.NonceByteSizes.MaxSize - AesGcm.TagByteSizes.MaxSize];
            using var aes = new AesGcm(key);
            aes.Decrypt(
                nonce, ciphertext.Slice(AesGcm.TagByteSizes.MaxSize + AesGcm.NonceByteSizes.MaxSize), tag, result
            );
            return result;
        }

        public byte[] Secp256K1Encrypt(Span<byte> recipientPublicKey, ReadOnlySpan<byte> plaintext)
        {
            var ephemeralPrivateKey = GeneratePrivateKey();
            var ephemeralPublicKey = new byte[64];
            var recipientPublicKeyParsed = new byte[64];
            if (!Secp256K1.PublicKeyCreate(ephemeralPublicKey, ephemeralPrivateKey))
                throw new Exception("Corrupted private key");
            if (!Secp256K1.PublicKeyParse(recipientPublicKeyParsed, recipientPublicKey))
                throw new Exception("Corrupted recipient public key");

            var sessionKey = new byte[32];
            if (!Secp256K1.Ecdh(sessionKey, recipientPublicKeyParsed, ephemeralPrivateKey))
                throw new Exception("Ecdh failed");

            var ephemeralPublicKeySerialized = new byte[33];
            if (!Secp256K1.PublicKeySerialize(ephemeralPublicKeySerialized, ephemeralPublicKey,
                Flags.SECP256K1_EC_COMPRESSED))
                throw new Exception("Corrupted public key");

            return ephemeralPublicKeySerialized.Concat(AesGcmEncrypt(sessionKey, plaintext)).ToArray();
        }

        public byte[] Secp256K1Decrypt(Span<byte> privateKey, Span<byte> ciphertext)
        {
            if (ciphertext.Length < 33) throw new ArgumentException("Ciphertext is too short", nameof(ciphertext));
            var senderPublicKeySerialized = ciphertext.Slice(0, 33);
            var senderPublicKey = new byte[64];
            if (!Secp256K1.PublicKeyParse(senderPublicKey, senderPublicKeySerialized))
                throw new ArgumentException("Invalid ciphertext: corrupted public key");

            var sessionKey = new byte[32];
            if (!Secp256K1.Ecdh(sessionKey, senderPublicKey, privateKey))
                throw new Exception("Ecdh failed");

            return AesGcmDecrypt(sessionKey, ciphertext.Slice(33));
        }
    }
}