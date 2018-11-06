using System;
using System.Collections.Generic;
using System.Linq;
using Neo;
using Neo.Network.P2P;
using Neo.Network.P2P.Payloads;
using Neo.Persistence.LevelDB;
using Neo.SmartContract;
using Neo.Wallets;
using Neo.Wallets.NEP6;

namespace test
{
    internal class Program
    {
        public static void Main(string[] args)
        {            
//            Wallet wallet = new NEP6Wallet();
//            Wallet wallet = new UserWallet(new WalletIndexer(), "wallet2.json", "two", false);
            var wallet = new NEP6Wallet(new WalletIndexer("Index_0000DDB1"), "wallet2.json");
            wallet.Unlock("two");

            var assetNEO = UInt256.Parse("0xc56f33fc6ecfcd0c225c4ab356fee59390af8560be0e930faebe74a6daff7c9b");
            var coins = wallet.FindUnspentCoins().Where(p => p.Output.AssetId.Equals(assetNEO)).ToArray();
            var tx = new ContractTransaction
            {
                Attributes = new TransactionAttribute[0],
                Inputs = coins.Select(p => p.Reference).ToArray(),
                Outputs = new[]
                {
                    new TransactionOutput
                    {
                        AssetId = assetNEO,
                        Value = Fixed8.FromDecimal(1),
                        ScriptHash = "AR3uEnLUdfm1tPMJmiJQurAXGL7h3EXQ2F".ToScriptHash()
                    }
                }
            };
            tx = wallet.MakeTransaction(tx);
            if (tx == null)
            {
                Console.WriteLine("Insufficient funds");
                return;
            } 
            ContractParametersContext context = new ContractParametersContext(tx);
            wallet.Sign(context);
            if (!context.Completed)
            {
                Console.WriteLine("SignatureContext:");
                Console.WriteLine(context.ToString());
                return;
            }
            tx.Witnesses = context.GetWitnesses();
            wallet.ApplyTransaction(tx);
            Console.WriteLine($"TXID: {tx.Hash}");

            var store = new LevelDBStore("Chain_0000DDB1");
            var result = tx.Verify(store.GetSnapshot(), new List<Transaction>());
            Console.WriteLine(result);
        }
        
        // "C:\Program Files\dotnet\dotnet.exe" "C:/Users/Dmitry Savonin/Documents/Blockchain/blockchain/neo-cli/bin/Debug/netcoreapp2.1/neo-cli.dll"
    }
}