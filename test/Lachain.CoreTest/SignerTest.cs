using System.Numerics;
using Google.Protobuf;
using Lachain.Core.Blockchain.Operations;
using Lachain.Core.Blockchain.SystemContracts.ContractManager;
using Lachain.Core.Blockchain.VM;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility.Utils;
using NUnit.Framework;

namespace Lachain.CoreTest
{
    public class SignerTest
    {
        [Test]
        public void Test_SignAndRecover()
        {
            var keyPair = new EcdsaKeyPair("0xD95D6DB65F3E2223703C5D8E205D98E3E6B470F067B0F94F6C6BF73D4301CE48".HexToBytes().ToPrivateKey());
            var signer = new TransactionSigner();
            var abi = ContractEncoder.Encode("hello()");
            var tx = new Transaction
            {
                To = ContractRegisterer.LatokenContract,
                Invocation = ByteString.CopyFrom(abi),
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = 123123,
                /* TODO: "calculate gas limit for input size" */
                GasLimit = GasMetering.DefaultBlockGasLimit,
                Nonce = 0,
                Value = new BigInteger(0).ToUInt256()
            };
            var receipt = signer.Sign(tx, keyPair);
            Assert.AreEqual(receipt.Hash.ToHex(), receipt.FullHash().ToHex());
            
            var publicKey = receipt.RecoverPublicKey();
            Assert.AreEqual(keyPair.PublicKey.ToHex(), publicKey.ToHex());
            Assert.AreEqual(keyPair.PublicKey.GetAddress().ToHex(), publicKey.GetAddress().ToHex());
        }

    }
}