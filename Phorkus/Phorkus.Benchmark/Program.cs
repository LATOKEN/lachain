using System;
using System.Threading;
using Google.Protobuf;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Blockchain.Pool;
using Phorkus.Core.Proto;
using Phorkus.Core.Utils;

namespace Phorkus.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            var validators = new[]
            {
                "0x02103a7f7dd016558597f7960d27c516a4394fd968b9e65155eb4b013e4040406e"
            };
            
            var genesisBuilder = new GenesisBuilder(
                new GenesisAssetsBuilder(null));
            var assetBuilder = new GenesisAssetsBuilder(null);
            var genesisBlock = genesisBuilder.Build();

            var lastTime = TimeUtils.CurrentTimeMillis();

            const int maxTx = 1000;
            var txs = new SignedTransaction[maxTx];
            for (var i = 0; i < maxTx; i++)
            {
                var transaction = assetBuilder.BuildGenesisMinerTransaction();
                txs[i] = new SignedTransaction
                {
                    Transaction = transaction,
                    Hash = transaction.ToHash256(),
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
                        var blockBuilder = new BlockBuilder(txs, genesisBlock.Block.Hash, genesisBlock.Block.Header.Index);
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
                System.Console.WriteLine("BPS: " + speed + " | TPS: " + speed * maxTx + " | TP: " + size / 1024 / 1024 +
                                         " mbyte/s");
                lastTime = currentTime;
                tps = 0;
                size = 0;
            }
        }
    }
}