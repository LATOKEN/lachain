using System;
using Google.Protobuf;
using Lachain.Proto;
using Lachain.Utility.Utils;
using Org.BouncyCastle.Bcpg.Sig;

namespace Lachain.Crypto
{
    public static class CryptoUtils
    {
        public const int PublicKeyLength = 33;
        public const int PrivateKeyLength = 32;
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        public static ECDSAPublicKey ToPublicKey(this byte[] buffer)
        {
            return new ECDSAPublicKey {Buffer = ByteString.CopyFrom(Crypto.DecodePublicKey(buffer, true))};
        }

        public static ECDSAPublicKey GetPublicKey(this ECDSAPrivateKey key)
        {
            return Crypto.ComputePublicKey(key.Encode(), true).ToPublicKey();
        }

        public static UInt160 GetAddress(this ECDSAPublicKey key)
        {
            return Crypto.ComputeAddress(key.EncodeCompressed()).ToUInt160();
        }

        public static byte[] EncodeCompressed(this ECDSAPublicKey key)
        {
            if (key.Buffer.Length != PublicKeyLength) throw new InvalidOperationException("Corrupted public key");
            return key.Buffer.ToByteArray();
        }

        public static ECDSAPrivateKey ToPrivateKey(this byte[] buffer)
        {
            if (buffer.Length != PrivateKeyLength)
                throw new ArgumentOutOfRangeException(nameof(buffer));
            return new ECDSAPrivateKey {Buffer = ByteString.CopyFrom(buffer)};
        }

        public static byte[] Encode(this ECDSAPrivateKey key)
        {
            if (key.Buffer.Length != 32) throw new InvalidOperationException("Corrupted private key");
            return key.Buffer.ToByteArray();
        }

        public static ECDSAPublicKey RecoverPublicKey(this TransactionReceipt receipt)
        {
            return Crypto.RecoverSignatureHashed(receipt.Hash.ToBytes(), receipt.Signature.Encode())
                .ToPublicKey();
        }
    }
}