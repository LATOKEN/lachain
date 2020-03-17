using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Phorkus.Core.Blockchain.Genesis;
using Phorkus.Core.Consensus;
using Phorkus.Core.RPC;
using Phorkus.Crypto;
using Phorkus.Crypto.MCL.BLS12_381;
using Phorkus.Networking;
using Phorkus.Storage;
using Phorkus.Utility.Utils;

namespace Phorkus.Console
{
    internal class Config
    {
        [JsonProperty("network")] public NetworkConfig Network { get; set; }
        [JsonProperty("genesis")] public GenesisConfig Genesis { get; set; }
        [JsonProperty("rpc")] public RpcConfig Rpc { get; set; }
        [JsonProperty("consensus")] public ConsensusConfig Consensus { get; set; }
        [JsonProperty("storage")] public StorageConfig Storage { get; set; }
    }

    class Program
    {
        static void TrustedKeyGen()
        {
            const int n = 22, f = 7;
            var tpkeKeyGen = new Crypto.TPKE.TrustedKeyGen(n, f);
            var tpkePubKey = tpkeKeyGen.GetPubKey();
            var tpkeVerificationKey = tpkeKeyGen.GetVerificationKey();

            var sigKeyGen = new Crypto.ThresholdSignature.TrustedKeyGen(n, f);
            var privShares = sigKeyGen.GetPrivateShares().ToArray();
            var pubShares = sigKeyGen.GetPrivateShares()
                .Select(s => s.GetPublicKeyShare())
                .Select(s => s.ToByteArray().ToHex())
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
                using var file = new StreamWriter($"config{i+1:D2}.json");
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
                    PrivateKey = ecdsaPrivateKeys[i],
                };
                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = 7070,
                };
                var consensus = new ConsensusConfig
                {
                    ValidatorsEcdsaPublicKeys = ecdsaPublicKeys.ToList(),
                    EcdsaPrivateKey = ecdsaPrivateKeys[i],
                    TpkePublicKey = tpkePubKey.ToByteArray().ToHex(),
                    TpkePrivateKey = tpkeKeyGen.GetPrivKey(i).ToByteArray().ToHex(),
                    TpkeVerificationKey = tpkeVerificationKey.ToByteArray().ToHex(),
                    ThresholdSignaturePrivateKey = privShares[i].ToByteArray().ToHex(),
                    ThresholdSignaturePublicKeySet = pubShares.ToList(),
                };
                var storage = new StorageConfig
                {
                    Path = "ChainPhorkus",
                    Provider = "RocksDB",
                };
                var config = new Config
                {
                    Network = net,
                    Genesis = genesis,
                    Rpc = rpc,
                    Consensus = consensus,
                    Storage = storage,
                };
                file.Write(JsonConvert.SerializeObject(config, Formatting.Indented));
                // System.Console.WriteLine($"Player {i} config:");
                // System.Console.WriteLine($"    \"TPKEPublicKey\": \"{tpkePubKey.ToByteArray().ToHex()}\",");
                // System.Console.WriteLine(
                //     $"    \"TPKEPrivateKey\": \"{tpkeKeyGen.GetPrivKey(i).ToByteArray().ToHex()}\",");
                // System.Console.WriteLine(
                //     $"    \"TPKEVerificationKey\": \"{tpkeVerificationKey.ToByteArray().ToHex()}\",");
                // System.Console.WriteLine($"    \"ThresholdSignaturePublicKeys\": [{pubShares}],");
                // System.Console.WriteLine(
                //     $"    \"ThresholdSignaturePrivateKey\": \"{privShares[i].ToByteArray().ToHex()}\"");
                // System.Console.WriteLine();
            }
        }

        internal static void Main(string[] args)
        {
            Mcl.Init();
            var app = new Application();
            app.Start(args);
        }
    }
}