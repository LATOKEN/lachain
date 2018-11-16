using System.Threading;
using Google.Protobuf;
using Phorkus.Core.Blockchain;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.OperationManager.TransactionManager;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var privateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
            var address = "0xe3c7a20ee19c0107b9121087bcba18eb4dcb8576".HexToBytes();
            var publicKey ="0x04affc3f22498bd1f70740b156faf8b6025269f55ee9e87f48b6fd95a33772fcd5529db79354bbace25f4f378d6a1320ae69994841ff6fb547f1b3a0c21cf73f68".HexToBytes();
            
//            var PrivateKey = "0xd95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48".HexToBytes();
//            var Address = "0x6bc32575acb8754886dc283c2c8ac54b1bd93195".HexToBytes();
//            var PublicKey ="0xe5974f3e1e9599ff5af036b5d6057d80855e7182afb4c2fa1fe38bc6efb9072b2c0b1382cc790ce4ad88c1d092d9432c63588fc089d56c522e6306dea55e1508".HexToBytes();

            var validators = new[]
            {
                "0x02103a7f7dd016558597f7960d27c516a4394fd968b9e65155eb4b013e4040406e"
            };
            var genesisAssetsBuilder = new GenesisAssetsBuilder(validators);
            var genesisBuilder = new GenesisBuilder(genesisAssetsBuilder);
            
            var crypto = new BouncyCastle();
            
            var registerTx = genesisAssetsBuilder.BuildGoverningTokenRegisterTransaction();
            var txManager = new TransactionManager(null, null, null, null, crypto);
            
            System.Console.Write("Signing transaction... ");
            var signed = txManager.Sign(new HashedTransaction(registerTx.Transaction), new KeyPair(privateKey, publicKey));
            System.Console.WriteLine(signed.Signature.Buffer.ToHex());
            var publicKey2 = new PublicKey
            {
                Buffer = ByteString.CopyFrom(publicKey)
            };
            System.Console.Write("Verifing signature... ");
            var result = txManager.VerifySignature(signed, publicKey2);
            System.Console.WriteLine(result);

            var invalidSigned = new SignedTransaction
            {
                Transaction = signed.Transaction,
                Hash = signed.Hash,
                Signature = new Signature
                {
                    Buffer = ByteString.CopyFrom("0x4a252a9a20db1fed75aaf5770857ee6d364ca7fbcefcd1dd7f3194aa59ae2cb1d22a3860900de65157ba625d80030ffdfc64d1cc872e94dc8aff43199fb6a7f5".HexToBytes())
                }
            };
            System.Console.Write("Verifing invalid signature... ");
            var result2 = txManager.VerifySignature(invalidSigned, publicKey2);
            System.Console.WriteLine(result2);
        }
        
        static void Benchmark(string[] args)
        {
            var validators = new[]
            {
                "0x02103a7f7dd016558597f7960d27c516a4394fd968b9e65155eb4b013e4040406e"
            };
            
            var genesisBuilder = new GenesisBuilder(
                new GenesisAssetsBuilder(validators));
            var assetBuilder = new GenesisAssetsBuilder(validators);
            var genesisBlock = genesisBuilder.Build();
            
            var lastTime = TimeUtils.CurrentTimeMillis();
            
            const int maxTx = 1000;
            var txs = new SignedTransaction[maxTx];
            for (var i = 0; i < maxTx; i++)
            {
                var hashed = assetBuilder.BuildGenesisMinerTransaction();
                txs[i] = new SignedTransaction
                {
                    Transaction = hashed.Transaction,
                    Hash = hashed.Hash,
                    Signature = SignatureUtils.Zero
                };
            }

            var size = 0;
            var tps = 0;
            
            var threads = new Thread[12];
            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    while (true)
                    {
                        var blockBuilder = new BlockBuilder(txs, genesisBlock.Block);
                        var block = blockBuilder.Build(0);
                        Interlocked.Add(ref size, block.Block.ToByteArray().Length);
                        Interlocked.Add(ref tps, 1);
                    }
                });
                threads[i].Start();
            }
            
            while (true)
            {
                Thread.Sleep(1000);
                var currentTime = TimeUtils.CurrentTimeMillis();
                var deltaTime = currentTime - lastTime;
                if (deltaTime < 1000)
                    continue;
                var speed = 1000 * tps / deltaTime;
                System.Console.WriteLine("BPS: " + speed + " | TPS: " + speed * maxTx + " | TP: " + size / 1024 / 1024 + " mbyte/s");
                lastTime = currentTime;
                tps = 0;
                size = 0;
            }
        }
    }
}