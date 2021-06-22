using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Lachain.Core.Blockchain.Operations;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;
using Nethereum.Util;
using System.Diagnostics;


namespace Lachain.UtilityTest
{
    public class TestUtils
    {
        public static TransactionReceipt GetRandomTransaction()
        {
            var signer = new TransactionSigner();
            byte[] random = new byte[32];
            RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            rng.GetBytes(random);
            var keyPair = new EcdsaKeyPair(random.ToPrivateKey());
            var randomValue = new Random().Next(1, 100);
            var tx = new Transaction
            {
                To = random.Slice(0, 20).ToUInt160(),
                From = keyPair.PublicKey.GetAddress(),
                GasPrice = (ulong) Money.Parse("0.0000001").ToWei(),
                GasLimit = 100000000,
                Nonce = 0,
                Value = Money.Parse($"{randomValue}.0").ToUInt256()
            };
            return signer.Sign(tx, keyPair);
        }

        public static void DeleteTestChainData()
        {
            var chainTest = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ChainTest");
            if (Directory.Exists(chainTest))  Directory.Delete(chainTest, true);
            Directory.CreateDirectory(chainTest);
            var chainTest2 = Path.Join(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "ChainTest2");
            if (Directory.Exists(chainTest2)) Directory.Delete(chainTest2, true);
            Directory.CreateDirectory(chainTest2);
        }
    }
}
