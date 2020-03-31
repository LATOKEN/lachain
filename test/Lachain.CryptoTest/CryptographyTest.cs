using System;
using System.Linq;
using System.Numerics;
using Google.Protobuf;
using Nethereum.Signer;
using NUnit.Framework;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Transaction = Lachain.Proto.Transaction;

namespace Lachain.CryptoTest
{
    public class CryptographyTest
    {
        private const int N = 2;

        [Test]
        public void Test_BouncyCastle_SignRoundTrip()
        {
            var crypto = new DefaultCrypto();
            var privateKey = "0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);
            var address = "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".HexToBytes();

            CollectionAssert.AreEqual(address, crypto.ComputeAddress(publicKey));

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                // var message = "0xdeadbeef" + it.ToString("X4");
                var message =
                    "0xec808504a817c800825208948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080";
                var digest = message.HexToBytes();
                var signature = crypto.Sign(digest, privateKey);
                Console.WriteLine(signature.ToHex());
                Console.WriteLine(signature.ToHex());
                Assert.IsTrue(crypto.VerifySignature(digest, signature, publicKey));
                var recoveredPubkey = crypto.RecoverSignature(digest, signature);
                Assert.AreEqual(recoveredPubkey, publicKey);
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }

        [Test]
        public void Test_External_Signature()
        {
            var crypto = CryptoProvider.GetCrypto();
            var message = "0xec098504a817c800825208943535353535353535353535353535353535353535880de0b6b3a764000080258080"
                .HexToBytes();
            var signature =
                "0x4f55924fbf85d5ccef03ff14bee357642f67aa7336740e4a369e62caff4b3f9c39d677b1fda7b2476e444f077752dade90707942a73bc87ad8f992ee8b84087426"
                    .HexToBytes();
            var pubKey = crypto.RecoverSignature(message, signature);
            Assert.AreEqual(
                "0x9d8a62f656a8d1615c1294fd71e9cfb3e4855a4f".ToLower(),
                crypto.ComputeAddress(pubKey).ToHex().ToLower()
            );
        }

        [Test]
        public void Test_EIP_155()
        {
            var crypto = CryptoProvider.GetCrypto();
            ulong nonce = 9;
            var nonceBig = new BigInteger(nonce);
            nonceBig.ToByteArray();
            var tx = new Transaction
            {
                Type = TransactionType.Transfer,
                To = "0x8E7B7262e0Fa4616566591d51F998f16A79fB547".HexToBytes().ToUInt160(),
                Value = "0x0de0b6b3a7640000".HexToBytes().ToUInt256(true),
                Nonce = 0,
                GasPrice = 20000000000,
                GasLimit = 3000000
            };

            var privateKey = "D95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes()
                .ToPrivateKey();
            var publicKey = privateKey.GetPublicKey();

            Sign(tx, new ECDSAKeyPair(privateKey, publicKey));

            // var message = "0xdeadbeef".HexToBytes();
            // var signature =
            //     "0x008cb79fb05605ffb79266395eec371f3b0d9e69b55512017acbfe5577884220ef4922d2d0d4ce0a0f3ee864aa3853b42fb319edab60f6294d2696cd4ed5517cf8"
            //         .HexToBytes();
            // var pubKey = crypto.RecoverSignature(message, signature);
            // Assert.AreEqual(
            //     crypto.ComputeAddress(pubKey).ToHex().ToLower(),
            //     "0x6Bc32575ACb8754886dC283c2c8ac54B1Bd93195".ToLower()
            // );
        }

        [Test]
        public void Test_Rlp()
        {
            var tx = new Transaction
            {
                Type = TransactionType.Transfer,
                To = "0x5f193b130d7c856179aa3d738ee06fab65e73147".HexToBytes().ToUInt160(),
                Value = Money.Parse("100").ToUInt256(),
                Nonce = 0,
                GasPrice = 5000000000,
                GasLimit = 4500000
            };
            var rlp = tx.Rlp();
            // compare with actual RLP from metamask
            Assert.IsTrue(rlp.SequenceEqual("0xee8085012a05f2008344aa20945f193b130d7c856179aa3d738ee06fab65e7314789056bc75e2d6310000080298080".HexToBytes()));
        }

        public void Sign(Transaction transaction, ECDSAKeyPair keyPair)
        {
            // Console.WriteLine(transaction.Nonce.ToBytes().ToUInt160().ToHex());
            /* use raw byte arrays to sign transaction hash */
            var ethTx = new Nethereum.Signer.Transaction(
                new BigInteger(transaction.Nonce).ToByteArray().Reverse().ToArray(),
                new BigInteger(transaction.GasPrice).ToByteArray().Reverse().ToArray(),
                new BigInteger(transaction.GasLimit).ToByteArray().Reverse().ToArray(),
                transaction.To.ToBytes(),
                transaction.Value.ToBytes(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                Array.Empty<byte>(),
                0);
            // ethTx.Sign();
            var rlp = ethTx.GetRLPEncodedRaw();
            Console.WriteLine("RLP " + rlp.ToHex());
            // var message = rlp.Keccak256();


            var crypto = CryptoProvider.GetCrypto();

            var signature = crypto.Sign(rlp, keyPair.PrivateKey.Encode());

            Console.WriteLine(signature.Take(32).ToHex());
            Console.WriteLine(signature.Skip(32).Take(32).ToHex());

            var r = signature.Take(32).ToArray().ToHex();
            var s = signature.Skip(32).Take(32).ToHex();
            Console.WriteLine(signature.Skip(32).Take(32).ToHex());

            Console.WriteLine(HexUtils.ToHex(signature.ToSignature()));
            // /* we're afraid */
            var pubKey = crypto.RecoverSignature(rlp, signature);
            if (!pubKey.SequenceEqual(keyPair.PublicKey.EncodeCompressed()))
                throw new InvalidKeyPairException();
        }


        [Test]
        public void Test_BouncyCastle_RecoverSignature()
        {
            var crypto = new DefaultCrypto();

            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var publicKey = crypto.ComputePublicKey(privateKey);

            var startTs = TimeUtils.CurrentTimeMillis();
            for (var it = 0; it < N; ++it)
            {
                var message = ("0xbadcab1e" + it.ToString("X4")).HexToBytes();
                var signature = crypto.Sign(message, privateKey);
                var recoveredPubkey = crypto.RecoverSignature(message, signature);
                CollectionAssert.AreEqual(recoveredPubkey.ToHex(), publicKey.ToHex());
            }

            var endTs = TimeUtils.CurrentTimeMillis();
            Console.WriteLine(endTs - startTs);
            Console.WriteLine((endTs - startTs) / N);
        }


        [Test]
        public void Test_External_Signature2()
        {
            var crypto = CryptoProvider.GetCrypto();

            var rawTx =
                "0xf86d808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a76400008025a0115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78a012fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33";

            var ethTx = new TransactionChainId(rawTx.HexToBytes());
            Console.WriteLine("ETH RLP: " + ethTx.GetRLPEncodedRaw().ToHex());

            var nonce = ethTx.Nonce.ToHex();

            Console.WriteLine("Nonce " + nonce);
            Console.WriteLine("ChainId " + Convert.ToUInt64(ethTx.ChainId.ToHex(), 16));

            var tx = new Transaction
            {
                Type = TransactionType.Transfer,
                To = ethTx.ReceiveAddress.ToUInt160(),
                Value = ethTx.Value.Reverse().ToArray().ToUInt256(true),
                Nonce = Convert.ToUInt64(ethTx.Nonce.ToHex(), 16),
                GasPrice = Convert.ToUInt64(ethTx.GasPrice.ToHex(), 16),
                GasLimit = Convert.ToUInt64(ethTx.GasLimit.ToHex(), 16)
            };

            Console.WriteLine("RLP: " + tx.Rlp().ToHex());

            var address = ethTx.Key.GetPublicAddress().HexToBytes();
            var from = ethTx.Key.GetPublicAddress().HexToBytes().ToUInt160();
            Console.WriteLine(address.ToHex());

            var r = "0x115105d96a43f41a5ea562bb3e591cbfa431a8cdae9c3030457adca2cb854f78".HexToBytes();
            var s = "0x12fb41922c53c73473563003667ed8e783359c91d95b42301e1955d530b1ca33".HexToBytes();
            var v = "0x25".HexToBytes();
            var signature = r.Concat(s).Concat(v).ToArray();

            Console.WriteLine(signature.ToHex());

            var message =
                "0xed808504a817c800832dc6c0948e7b7262e0fa4616566591d51f998f16a79fb547880de0b6b3a764000080018080"
                    .HexToBytes().KeccakBytes();

            var recoveredPubkey = crypto.RecoverSignatureHashed(message, signature);

            Console.WriteLine(recoveredPubkey.ToHex());

            var addr = crypto.ComputeAddress(recoveredPubkey);
            var same = addr.SequenceEqual(from.ToBytes());
            Console.WriteLine(addr.ToHex());
            Console.WriteLine(same);
        }
    }
}