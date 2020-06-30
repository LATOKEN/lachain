using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.Core.Blockchain;
using Newtonsoft.Json;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.RPC;
using Lachain.Core.RPC.HTTP;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;

namespace Lachain.Console
{
    internal class Config
    {
        public Config(NetworkConfig network, GenesisConfig genesis, RpcConfig rpc, VaultConfig vault,
            StorageConfig storage, BlockchainConfig blockchain)
        {
            Network = network;
            Genesis = genesis;
            Rpc = rpc;
            Vault = vault;
            Storage = storage;
            Blockchain = blockchain;
        }

        [JsonProperty("network")] public NetworkConfig Network { get; set; }
        [JsonProperty("genesis")] public GenesisConfig Genesis { get; set; }
        [JsonProperty("rpc")] public RpcConfig Rpc { get; set; }
        [JsonProperty("vault")] public VaultConfig Vault { get; set; }
        [JsonProperty("storage")] public StorageConfig Storage { get; set; }
        [JsonProperty("blockchain")] public BlockchainConfig Blockchain { get; set; }
    }

    class Program
    {
        static void TrustedKeyGen()
        {
            const int n = 22, f = 7;
            // const int n = 4, f = 1;
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();

            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToHex())
                .ToArray();

            // var ips = new[]
            // {
            //     "116.203.75.72", "78.46.123.99", "95.217.4.100", "88.99.190.27", "78.46.229.200", "95.217.6.171",
            //     "88.99.190.191", "94.130.78.183", "94.130.24.163", "94.130.110.127", "94.130.110.95", "94.130.58.63",
            //     "88.99.86.166", "88.198.78.106", "88.198.78.141", "88.99.126.144", "88.99.87.58", "95.217.6.234",
            //     "95.217.12.226", "95.217.14.117", "95.217.17.248", "95.217.12.230"
            // };

            var ips = new[]
            {
                "116.203.75.72", "178.128.113.97", "165.227.45.119", "206.189.137.112", "157.245.160.201",
                "95.217.6.171", "88.99.190.191", "94.130.78.183", "94.130.24.163", "94.130.110.127", "94.130.110.95",
                "94.130.58.63", "88.99.86.166", "88.198.78.106", "88.198.78.141", "88.99.126.144", "88.99.87.58",
                "95.217.6.234", "95.217.12.226", "95.217.14.117", "95.217.17.248", "95.217.12.230"
            };

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
                    MyHost = $"tcp://{ips[i]}",
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
                        ecdsaPublicKeys[j], pubShares[j], ips[j]
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
                };
                var walletPath = GetCommandLineArgument("wallet");
                var vault = new VaultConfig
                {
                    Path = walletPath,
                    Password = "12345"
                    // EcdsaPrivateKey = ecdsaPrivateKeys[i],
                    // TpkePrivateKey = tpkeKeyGen.GetPrivKey(i).ToByteArray().ToHex(),
                    // ThresholdSignaturePrivateKey = privShares[i].ToByteArray().ToHex(),
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

        internal static void Main(string[] args)
        {
            // GenWallet(
            //     "wallet.json", 
            //     "d95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48", 
            //     "0x000000000000000000000000000000000000000000000000000000000000000000000000",
            //     "0xcb436d851f7d58773a36daf94350f25635b06fb970dc670059529f6b3797b668"
            // );
            // GenWallet(
            //     "wallet0.json", 
            //     "0a63d1202aa7b5052e2a823eb3873ee9f53709e967778bf39efcf1ea05bb3907", 
            //     "0x000000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xbc055e0f7bc72bdf9b0129f85c925eff36c4b8da5a6235a4b33b2e1131c24c20"
            // );
            // GenWallet(
            //     "wallet1.json", 
            //     "7125553f3ffbaa1a0e6b8787f1ad060e201adb338a28e838f93fb06f6b7fc5de", 
            //     "0x010000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xd12e210c67814de8aed0fe42ed7ce846803f7fcfeb163ab4c38aa7bd147bf823"
            // );
            // GenWallet(
            //     "wallet2.json", 
            //     "49ace3986253b6e0300ca1fe486ece278d557e896b1f7f446d498586913b9bf4", 
            //     "0x020000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xe657e408533b6ff1c19fd48d7d67728ec9ba45c47ccb3ec4d3d9206af833a427"
            // );
            // GenWallet(
            //     "wallet3.json", 
            //     "68fcd0c7ee8ca176ef46dd2c2296f20ae17e1046dd36a7a8ff25c0f9bde6002a", 
            //     "0x030000009199c5d2300431458cf806b5658420ce024089d4a788878b1582fe99e524c839",
            //     "0xfb80a7053ff590fad46eaad80d52fcd512360cb90d8043d4e3289a16dcec4f2b"
            // );
            if (IsArgumentPassed("version"))
            {
                System.Console.WriteLine(NodeService.GetNodeVersion());
                return;
            }

            var configPath = GetCommandLineArgument("config");
            using var app = new Application(configPath);
            app.Start(args);
        }

        public static string GetCommandLineArgument(string name, string? defaultValue = null)
        {
            defaultValue ??= name + ".json";
            var value = defaultValue;
            string[] arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length; i++)
            {
                if (arguments[i] == "--" + name && arguments.Length > i + 1)
                {
                    value = arguments[i + 1];
                }
            }

            return value;
        }

        public static bool IsArgumentPassed(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            return arguments.Any(arg => arg == "--" + name);
        }
    }
}