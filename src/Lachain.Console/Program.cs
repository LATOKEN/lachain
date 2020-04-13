using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.RPC;
using Lachain.Core.Vault;
using Lachain.Crypto;
using Lachain.Crypto.MCL.BLS12_381;
using Lachain.Networking;
using Lachain.Storage;
using Lachain.Utility.Utils;

namespace Lachain.Console
{
    internal class Config
    {
        [JsonProperty("network")] public NetworkConfig Network { get; set; }
        [JsonProperty("genesis")] public GenesisConfig Genesis { get; set; }
        [JsonProperty("rpc")] public RpcConfig Rpc { get; set; }
        [JsonProperty("vault")] public VaultConfig Vault { get; set; }
        [JsonProperty("storage")] public StorageConfig Storage { get; set; }
    }

    class Program
    {
        static void TrustedKeyGen()
        {
            // const int n = 22, f = 7;
            const int n = 4, f = 1;
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();
            
            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToBytes().ToHex())
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
            var crypto = CryptoProvider.GetCrypto();
            for (var i = 0; i < n; ++i)
            {
                ecdsaPrivateKeys[i] = crypto.GenerateRandomBytes(32).ToHex(false);
                ecdsaPublicKeys[i] = crypto.ComputePublicKey(ecdsaPrivateKeys[i].HexToBytes(), true).ToHex(false);
            }

            var peers = ips.Zip(ecdsaPublicKeys)
                .Select((t, i) => $"tcp://{t.Second}@{t.First}:5050")
                .ToArray();

            for (var i = 0; i < n; ++i)
            {
                var net = new NetworkConfig
                {
                    Magic = 56754,
                    Port = 5050,
                    MyHost = $"tcp://{ips[i]}",
                    Address = "0.0.0.0",
                    Peers = peers,
                    MaxPeers = 100,
                    ForceIPv6 = false
                };
                var genesis = new GenesisConfig
                {
                    Balances = new Dictionary<string, string>
                    {
                        {
                            "0x6bc32575acb8754886dc283c2c8ac54b1bd93195", "1000000"
                        }
                    },
                    Validators = Enumerable.Range(0, n).Select(j => new GenesisConfig.ValidatorInfo
                    {
                        ResolvableName = ips[j],
                        EcdsaPublicKey = ecdsaPublicKeys[j],
                        ThresholdSignaturePublicKey = pubShares[j]
                    }).ToList(),
                    ThresholdEncryptionPublicKey = tpkePubKey.ToBytes().ToHex()
                };
                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = 7070,
                };
                var vault = new VaultConfig
                {
                    Path = "wallet.json",
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
                var config = new Config
                {
                    Network = net,
                    Genesis = genesis,
                    Rpc = rpc,
                    Vault = vault,
                    Storage = storage,
                };
                File.WriteAllText($"config{i + 1:D2}.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                GenWallet(
                    $"wallet{i + 1:D2}.json",
                    ecdsaPrivateKeys[i],
                    tpkeKeyGen.GetPrivKey(i).ToByteArray().ToHex(),
                    privShares[i].ToByteArray().ToHex()
                );
            }
        }

        private static void GenWallet(string path, string ecdsaKey, string tpkeKey, string tsKey)
        {
            var config = new JsonWallet
            {
                EcdsaPrivateKey = ecdsaKey,
                ThresholdSignatureKeys = new Dictionary<ulong, string>
                {
                    {0, tsKey}
                },
                TpkePrivateKeys = new Dictionary<ulong, string>
                {
                    {0, tpkeKey}
                }
            };
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            var passwordHash = Encoding.UTF8.GetBytes("12345").KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            File.WriteAllBytes(path, crypto.AesGcmEncrypt(passwordHash, json));
        }

        internal static void Main(string[] args)
        {
            Mcl.Init();
            // GenWallet(
            //     "wallet.json", 
            //     "d95d6db65f3e2223703c5d8e205d98e3e6b470f067b0f94f6c6bf73d4301ce48", 
            //     "0x000000000000000000000000000000000000000000000000000000000000000000000000",
            //     "0xcb436d851f7d58773a36daf94350f25635b06fb970dc670059529f6b3797b668"
            // );
            var app = new Application();
            app.Start(args);
        }
    }
}