﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NLog;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Sec;
using Org.BouncyCastle.Asn1.X9;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC;
using Org.BouncyCastle.Security;
using Phorkus.Logger;
using Phorkus.Utility.Utils;
using Secp256k1Net;

namespace Phorkus.Crypto
{
    public class BouncyCastle : ICrypto
    {
        private static readonly X9ECParameters Curve = SecNamedCurves.GetByName("secp256k1");
        private readonly ILogger<BouncyCastle> _logger = LoggerFactory.GetLoggerForClass<BouncyCastle>();


        private static readonly ECDomainParameters Domain
            = new ECDomainParameters(Curve.Curve, Curve.G, Curve.N, Curve.H, Curve.GetSeed());

        private static readonly Secp256k1 Secp256K1 = new Secp256k1();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool VerifySignature(byte[] message, byte[] signature, byte[] publicKey)
        {
            var pk = new byte[64];
            if (!Secp256K1.PublicKeyParse(pk, publicKey))
                throw new ArgumentException();

            var messageHash = System.Security.Cryptography.SHA256.Create().ComputeHash(message);

            var parsedSig = new byte[65];
            int recId = signature[0];
            if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Skip(1).ToArray(), recId))
                throw new ArgumentException();

            return Secp256K1.Verify(parsedSig.Take(64).ToArray(), messageHash, pk);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] Sign(byte[] message, byte[] privateKey)
        {
            var messageHash = message.Keccak256();

            var sig = new byte[65];
            if (!Secp256K1.SignRecoverable(sig, messageHash, privateKey))
                throw new ArgumentException();
            var serialized = new byte[64];
            if (!Secp256K1.RecoverableSignatureSerializeCompact(serialized, out var recId, sig))
                throw new ArgumentException();

            return new[] {(byte) recId}.Concat(serialized).ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public byte[] RecoverSignature(byte[] message, byte[] signature)
        {
            var messageHash = System.Security.Cryptography.SHA256.Create().ComputeHash(message);
            var parsedSig = new byte[65];
            var pk = new byte[64];
            int recId = signature[0];
            if (!Secp256K1.RecoverableSignatureParseCompact(parsedSig, signature.Skip(1).ToArray(), recId))
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
            return DecodePublicKey(publicKey, false, out _, out _).Skip(1).Keccak256().Skip(12).ToArray();
        }

        private static BigInteger CalculateE(BigInteger n, byte[] message)
        {
            var messageBitLength = message.Length * 8;
            var trunc = new BigInteger(1, message);
            if (n.BitLength < messageBitLength)
                trunc = trunc.ShiftRight(messageBitLength - n.BitLength);
            return trunc;
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

        public byte[] DecodePublicKey(byte[] publicKey, bool compress, out System.Numerics.BigInteger x,
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

        public byte[] AesEncrypt(byte[] data, byte[] key)
        {
            if (data == null || data.Length % 16 != 0) throw new ArgumentException(nameof(data));
            if (key == null || key.Length != 32) throw new ArgumentException(nameof(key));

            var cipher = CipherUtilities.GetCipher("AES/ECB/NoPadding");
            cipher.Init(true, ParameterUtilities.CreateKeyParameter("AES", key));

            return cipher.DoFinal(data);
        }

        public byte[] AesDecrypt(byte[] data, byte[] key)
        {
            if (data == null || data.Length % 16 != 0) throw new ArgumentException(nameof(data));
            if (key == null || key.Length != 32) throw new ArgumentException(nameof(key));

            var cipher = CipherUtilities.GetCipher("AES/ECB/NoPadding");
            cipher.Init(false, ParameterUtilities.CreateKeyParameter("AES", key));

            return cipher.DoFinal(data);
        }

        public byte[] AesEncrypt(byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || data.Length % 16 != 0) throw new ArgumentException(nameof(data));
            if (key == null || key.Length != 32) throw new ArgumentException(nameof(key));
            if (iv == null || iv.Length != 16) throw new ArgumentException(nameof(iv));

            var cipher = CipherUtilities.GetCipher("AES/CBC/NoPadding");
            cipher.Init(true, new ParametersWithIV(ParameterUtilities.CreateKeyParameter("AES", key), iv));

            return cipher.DoFinal(data);
        }

        public byte[] AesDecrypt(byte[] data, byte[] key, byte[] iv)
        {
            if (data == null || data.Length % 16 != 0) throw new ArgumentException(nameof(data));
            if (key == null || key.Length != 32) throw new ArgumentException(nameof(key));
            if (iv == null || iv.Length != 16) throw new ArgumentException(nameof(iv));

            var cipher = CipherUtilities.GetCipher("AES/CBC/NoPadding");
            cipher.Init(false, new ParametersWithIV(ParameterUtilities.CreateKeyParameter("AES", key), iv));

            return cipher.DoFinal(data);
        }

        public byte[] SCrypt(byte[] P, byte[] S, int N, int r, int p, int dkLen)
        {
            if (P == null) throw new ArgumentException(nameof(P));
            if (S == null) throw new ArgumentException(nameof(S));
            if ((N & (N - 1)) != 0 || N < 2 || N >= Math.Pow(2, 128 * r / 8)) throw new ArgumentException(nameof(N));
            if (r < 1) throw new ArgumentException(nameof(r));
            if (p < 1 || p > int.MaxValue / (128 * r * 8)) throw new ArgumentException(nameof(p));
            if (dkLen < 1) throw new ArgumentException(nameof(dkLen));

            return Org.BouncyCastle.Crypto.Generators.SCrypt.Generate(P, S, N, r, p, dkLen);
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