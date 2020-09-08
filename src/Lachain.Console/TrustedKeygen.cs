using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.RPC;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Newtonsoft.Json;

namespace Lachain.Console
{
    public class TrustedKeygen
    {
        public static void DoKeygen(int n, int f, IEnumerable<string> ips)
        {
            if (n <= 3 * f) throw new Exception("N must be >= 3 * F + 1");
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();

            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToHex())
                .ToArray();
            
            var ecdsaPrivateKeys = new string[n];
            var ecdsaPublicKeys = new string[n];
            var addresses = new string[n];
            var crypto = CryptoProvider.GetCrypto();
            for (var i = 0; i < n; ++i)
            {
                ecdsaPrivateKeys[i] = crypto.GenerateRandomBytes(32).ToHex(false);
                ecdsaPublicKeys[i] = crypto.ComputePublicKey(ecdsaPrivateKeys[i].HexToBytes(), true).ToHex(false);
                addresses[i] = ecdsaPrivateKeys[i].HexToBytes().ToPrivateKey().GetPublicKey().GetAddress().ToHex();
            }

            var peers = ips.Zip(ecdsaPublicKeys)
                .Select((t, i) => $"tcp://{t.Second}@{t.First}:5050")
                .ToArray();

            for (var i = 0; i < n; ++i)
            {
                var net = new NetworkConfig
                {
                    Port = 5050,
                    Address = "0.0.0.0",
                    Peers = peers,
                    MaxPeers = 100,
                    ForceIPv6 = false
                };
                var genesis = new GenesisConfig(tpkePubKey.ToHex(), "5.000000000000000000", "0.000000100000000000")
                {
                    Balances = new Dictionary<string, string>
                    {
                        {
                            "0x6bc32575acb8754886dc283c2c8ac54b1bd93195", "1000000"
                        }
                    },
                    Validators = Enumerable.Range(0, n).Select(j => new ValidatorInfo(
                        ecdsaPublicKeys[j], pubShares[j]
                    )).ToList()
                };
                for (var j = 0; j < n; ++j)
                {
                    genesis.Balances[addresses[j]] = "1000000";
                }

                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = 7070,
                    ApiKey = "asdasdasd",
                };
                var walletPath = "wallet.json";
                var vault = new VaultConfig
                {
                    Path = walletPath,
                    Password = "12345"
                };
                var storage = new StorageConfig
                {
                    Path = "ChainLachain",
                    Provider = "RocksDB",
                };
                var blockchain = new BlockchainConfig();
                var config = new Config(net, genesis, rpc, vault, storage, blockchain);
                File.WriteAllText($"config{i + 1:D2}.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                GenWallet(
                    $"wallet{i + 1:D2}.json",
                    ecdsaPrivateKeys[i],
                    tpkeKeyGen.GetPrivKey(i).ToHex(),
                    privShares[i].ToHex()
                );
            }

            var tpkePrivKeys = string.Join(
                ", ",
                Enumerable.Range(0, n)
                    .Select(idx => tpkeKeyGen.GetPrivKey(idx))
                    .Select(x => $"\"{x.ToHex()}\""));
            var tsKeys = string.Join(
                ", ",
                sigKeyGen.GetPrivateShares()
                    .Select(x => $"(\"{x.GetPublicKeyShare().ToHex()}\", \"{x.ToHex()}\")")
            );
            System.Console.WriteLine(
                $"{n}: " + "{" +
                "  \"tpke\": (" +
                $"    \"{tpkePubKey.ToHex()}\"," +
                $"    [{tpkePrivKeys}]" +
                "  )," +
                $"  \"ts\": [{tsKeys}]," +
                "}");
            System.Console.WriteLine(
                string.Join(", ", ecdsaPrivateKeys.Zip(ecdsaPublicKeys).Zip(addresses)
                    .Select(t => $"(\"{t.First.Second}\", \"{t.First.First}\", \"{t.Second}\")"))
            );
        }
        
        private static void GenWallet(string path, string ecdsaKey, string tpkeKey, string tsKey)
        {
            var config = new JsonWallet(
                ecdsaKey,
                new Dictionary<ulong, string> {{0, tpkeKey}},
                new Dictionary<ulong, string> {{0, tsKey}}
            );
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            var passwordHash = Encoding.UTF8.GetBytes("12345").KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            File.WriteAllBytes(path, crypto.AesGcmEncrypt(passwordHash, json));
        }
    }
}