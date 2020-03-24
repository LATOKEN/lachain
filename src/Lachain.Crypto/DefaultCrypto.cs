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
            return DecodePublicKey(publicKey, false, out _, out _).Skip(1).KeccakBytes().Skip(12).ToArray();
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

        private byte[] DecodePublicKey(byte[] publicKey, bool compress, out System.Numerics.BigInteger x,
            out System.Numerics.BigInteger y)
        {
            if (publicKey == null || publicKey.Length != 33 && publicKey.Length != 64 && publicKey.Length != 65)
                throw new ArgumentException(nameof(publicKey));

            switch (publicKey.Length)
            {
                case 33 when publicKey[0] != 0x02 && publicKey[0] != 0x03:
                    throw new ArgumentException(nameof(publicKey));
                case 65 when publicKey[0] != 0x04:
                    throw new ArgumentException(nameof(publicKey));
            }

            byte[] fullPublicKey;

            if (publicKey.Length == 64)
            {
                fullPublicKey = new byte[65];
                fullPublicKey[0] = 0x04;
                Array.Copy(publicKey, 0, fullPublicKey, 1, publicKey.Length);
            }
            else
            {
                fullPublicKey = publicKey;
            }

            var ret = new ECPublicKeyParameters("ECDSA", Curve.Curve.DecodePoint(fullPublicKey), Domain).Q;
            var x0 = ret.XCoord.ToBigInteger();
            var y0 = ret.YCoord.ToBigInteger();

            x = System.Numerics.BigInteger.Parse(x0.ToString());
            y = System.Numerics.BigInteger.Parse(y0.ToString());

            return ret.GetEncoded(compress);
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