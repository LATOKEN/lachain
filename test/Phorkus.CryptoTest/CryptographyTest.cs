using System;
using System.IO;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Nethereum.Signer;
using NUnit.Framework;
using Phorkus.Core.Blockchain.OperationManager;
using Phorkus.Core.Config;
using Phorkus.Core.DI;
using Phorkus.Core.DI.Modules;
using Phorkus.Core.DI.SimpleInjector;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Storage.State;
using Phorkus.Utility.Utils;
using Transaction = Phorkus.Proto.Transaction;

namespace Phorkus.CryptoTest
{
    public class CryptographyTest
    {
        private const int N = 1024;

        private readonly IContainer _container;

        [Test]
        public void Test_BouncyCastle_SignRoundTrip()
        {
            var crypto = new BouncyCastle();
            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);
            var address = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes();

            CollectionAssert.AreEqual(address, crypto.ComputeAddress(publicKey));

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                var message = "0xdeadbeef" + it.ToString("X4");
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, privateKey);
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature);
                CollectionAssert.AreEqual(recoveredPubkey, publicKey);
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }

        [Test]
        public void Test_External_Signature()
        {
            var crypto = new BouncyCastle();
            var message = "0xdeadbeef".HexToBytes();
            var signature =
                "0x008cb79fb05605ffb79266395eec371f3b0d9e69b55512017acbfe5577884220ef4922d2d0d4ce0a0f3ee864aa3853b42fb319edab60f6294d2696cd4ed5517cf8"
                    .HexToBytes();
            var pubKey = crypto.RecoverSignature(message, signature);
            Assert.AreEqual(
                HexUtils.ToHex(crypto.ComputeAddress(pubKey)).ToLower(),
                "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower()
            );
        }

        [Test]
        public void Test_EIP_155()
        {
            var crypto = new BouncyCastle();
            ulong nonce = 9;
            var nonceBig = new BigInteger(nonce);
            nonceBig.ToByteArray();
            var tx = new Transaction
            {
                Type = TransactionType.Transfer,
                To = new UInt160
                {
                    Buffer = ByteString.CopyFrom("0x3535353535353535353535353535353535353535".HexToBytes())
                },
                Value = new UInt256
                {
                    Buffer = ByteString.CopyFrom("0x0de0b6b3a7640000".HexToBytes())
                },
                Nonce = 9,
                GasPrice = 20000000000,
                GasLimit = 21000
            };

            var privateKey = new ECDSAPrivateKey
            {
                Buffer = ByteString.CopyFrom("0x4646464646464646464646464646464646464646464646464646464646464646"
                    .HexToBytes())
            };

            var publicKey = new ECDSAPublicKey
            {
                Buffer = ByteString.CopyFrom(
                    crypto.ComputePublicKey("0x4646464646464646464646464646464646464646464646464646464646464646"
                        .HexToBytes()))
            };

            Sign(tx, new ECDSAKeyPair(privateKey, publicKey));

            var message = "0xdeadbeef".HexToBytes();
            var signature =
                "0x008cb79fb05605ffb79266395eec371f3b0d9e69b55512017acbfe5577884220ef4922d2d0d4ce0a0f3ee864aa3853b42fb319edab60f6294d2696cd4ed5517cf8"
                    .HexToBytes();
            var pubKey = crypto.RecoverSignature(message, signature);
            Assert.AreEqual(
                HexUtils.ToHex(crypto.ComputeAddress(pubKey)).ToLower(),
                "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower()
            );
        }

        public void Sign(Transaction transaction, ECDSAKeyPair keyPair)
        {

            // Console.WriteLine(transaction.Nonce.ToBytes().ToUInt160().Buffer.ToHex());
            /* use raw byte arrays to sign transaction hash */
            var ethTx = new Nethereum.Signer.Transaction(
                new BigInteger(transaction.Nonce).ToByteArray().Reverse().ToArray(),
                new BigInteger(transaction.GasPrice).ToByteArray().Reverse().ToArray(),
                new BigInteger(transaction.GasLimit).ToByteArray().Reverse().ToArray(),
                transaction.To.Buffer.ToByteArray(),
                transaction.Value.Buffer.ToByteArray(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                37);

            Console.WriteLine();
            var rlp = ethTx.GetRLPEncoded();
            Console.WriteLine(HexUtils.ToHex(rlp));
            // var message = rlp.Keccak256();

            Console.WriteLine(HexUtils.ToHex(rlp));
            
            var crypto = new BouncyCastle();

            // var message = transaction.ToHash256().Buffer.ToByteArray();
            var signature = crypto.Sign(rlp, keyPair.PrivateKey.Buffer.ToByteArray());
            
            // EthECDSASignature ethSignature = new EthECDSASignature(r, s, v);
            
            // ethTx.SetSignature(signature);
            Console.WriteLine(HexUtils.ToHex(signature.ToSignature()));
            // /* we're afraid */
            // var pubKey = _crypto.RecoverSignature(message, signature);
            // if (!pubKey.SequenceEqual(keyPair.PublicKey.Buffer.ToByteArray()))
            //     throw new InvalidKeyPairException();
            // var signed = new TransactionReceipt
            // {
            //     Transaction = transaction,
            //     Hash = transaction.ToHash256(),
            //     Signature = signature.ToSignature()
            // };
            // OnTransactionSigned?.Invoke(this, signed);
            // return signed;
        }


        [Test]
        public void Test_BouncyCastle_RecoverSignature()
        {
            var crypto = new BouncyCastle();

            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                var message = ("0xbadcab1e" + it.ToString("X4")).HexToBytes();
                var signature = crypto.Sign(message, privateKey);
                var recoveredPubkey = crypto.RecoverSignature(message, signature);
                CollectionAssert.AreEqual(HexUtils.ToHex(recoveredPubkey), HexUtils.ToHex(publicKey));
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }
    }
}