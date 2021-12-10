using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.CommunicationHub.Net;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Genesis;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.Config;
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
        internal class Config
        {
            public Config(NetworkConfig network, GenesisConfig genesis, RpcConfig rpc, VaultConfig vault,
                StorageConfig storage, BlockchainConfig blockchain, HardforkConfig hardfork, VersionConfig version)
            {
                Network = network;
                Genesis = genesis;
                Rpc = rpc;
                Vault = vault;
                Storage = storage;
                Blockchain = blockchain;
                Hardfork = hardfork;
                ConfigVersion = version;
            }

            [JsonProperty("network")] public NetworkConfig Network { get; set; }
            [JsonProperty("genesis")] public GenesisConfig Genesis { get; set; }
            [JsonProperty("rpc")] public RpcConfig Rpc { get; set; }
            [JsonProperty("vault")] public VaultConfig Vault { get; set; }
            [JsonProperty("storage")] public StorageConfig Storage { get; set; }
            [JsonProperty("blockchain")] public BlockchainConfig Blockchain { get; set; }
            [JsonProperty("hardfork")] public HardforkConfig Hardfork { get; set; }
            [JsonProperty("version")] public VersionConfig ConfigVersion { get; set; }
        }

        public static void DoKeygen(int n, int f, IEnumerable<string> ips, ushort basePort, ushort target, ulong chainId, ulong cycleDuration, ulong validatorsCount, string networkName, 
            string feedAddress, string feedBalance, string stakeAmount)
        {
            if (ips.Any())
            {
                CloudKeygen(n, f, ips, basePort, target, chainId, cycleDuration, validatorsCount, networkName, feedAddress, feedBalance, stakeAmount);
            }
            else
            {
                LocalKeygen(n, f, basePort, target, chainId, cycleDuration, validatorsCount, networkName, feedAddress, feedBalance, stakeAmount);
            }
        }

        public static void CloudKeygen(int n, int f, IEnumerable<string> ips, ushort basePort, ushort target, ulong chainId, ulong cycleDuration, ulong validatorsCount, string networkName, 
            string feedAddress, string feedBalance, string stakeAmount)
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

            var hubPublicKeys = new string[n];
            var serializedHubPrivateKeys = new string[n];
            for (var i = 0; i < n; ++i)
            {
                (serializedHubPrivateKeys[i], hubPublicKeys[i]) = PrivateWallet.GenerateHubKey();
            }

            var bootstraps = ips
                .Zip(hubPublicKeys, (ip, id) => $"{id}@{ip}")
                .Select((x, i) => $"{x}:{41011 + i}")
                .ToArray();

            var peers = ecdsaPublicKeys.ToArray();
            
            for (var i = 0; i < n; ++i)
            {
                var net = new NetworkConfig
                {
                    Peers = peers,
                    MaxPeers = 100,
                    ForceIPv6 = false,
                    BootstrapAddresses = bootstraps,
                    HubLogLevel = "Trace",
                    HubMetricsPort = basePort + 2,
                    NetworkName = networkName,
                    ChainId = (int)chainId,
                    CycleDuration = cycleDuration,
                    ValidatorsCount = validatorsCount,
                };
                var genesis = new GenesisConfig(tpkePubKey.ToHex(), "5.000000000000000000", "0.000000100000000000")
                {
                    Balances = new Dictionary<string, string>
                    {
                        {
                            feedAddress, feedBalance
                        }
                    },
                    Validators = Enumerable.Range(0, n).Select(j => new ValidatorInfo(
                        ecdsaPublicKeys[j], pubShares[j], feedAddress, stakeAmount
                    )).ToList()
                };
                for (var j = 0; j < n; ++j)
                {
                    genesis.Balances[addresses[j]] = "100";
                }

                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = basePort,
                    MetricsPort = (ushort) (basePort + 1),
                    ApiKey = "asdasdasd",
                };
                var walletPath = "wallet.json";
                var vault = new VaultConfig
                {
                    Path = walletPath,
                    Password = getRandomPassword()
                };
                var storage = new StorageConfig
                {
                    Path = "ChainLachain",
                    Provider = "RocksDB",
                };
                var blockchain = new BlockchainConfig
                {
                    TargetBlockTime = target,
                };
                var hardfork = new HardforkConfig
                {
                    Hardfork_1 = 0,
                };
                var version = new VersionConfig
                {
                    Version = 2
                };
                var config = new Config(net, genesis, rpc, vault, storage, blockchain, hardfork, version);
                File.WriteAllText($"config{i + 1:D2}.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                GenWallet(
                    $"wallet{i + 1:D2}.json",
                    ecdsaPrivateKeys[i],
                    serializedHubPrivateKeys[i],
                    tpkeKeyGen.GetPrivKey(i).ToHex(),
                    privShares[i].ToHex(),
                    vault.Password
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

        public static void LocalKeygen(int n, int f, int basePort, ushort target, ulong chainId, ulong cycleDuration, ulong validatorsCount, string networkName, 
            string feedAddress, string feedBalance, string stakeAmount)
        {
            System.Console.WriteLine($"chainId : {chainId}");
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

            var hubPublicKeys = new string[n];
            var serializedHubPrivateKeys = new string[n];
            for (var i = 0; i < n; ++i)
            {
                (serializedHubPrivateKeys[i], hubPublicKeys[i]) = PrivateWallet.GenerateHubKey();
            }

            var ips = Enumerable.Repeat("127.0.0.1", n);
            var bootstraps = ips
                .Zip(hubPublicKeys, (ip, id) => $"{id}@{ip}")
                .Select((x, i) => $"{x}:{41011 + i}")
                .ToArray();

            var peers = ecdsaPublicKeys.ToArray();

            for (var i = 0; i < n; ++i)
            {
                var net = new NetworkConfig
                {
                    Peers = peers,
                    MaxPeers = 100,
                    ForceIPv6 = false,
                    BootstrapAddresses = bootstraps,
                    HubLogLevel = "Trace",
                    HubMetricsPort = basePort + 2 * n + i,
                    NetworkName = networkName,
                    ChainId = (int)chainId,
                    CycleDuration = cycleDuration,
                    ValidatorsCount = validatorsCount,

                };
                var genesis = new GenesisConfig(tpkePubKey.ToHex(), "5.000000000000000000", "0.000000100000000000")
                {
                    Balances = new Dictionary<string, string>
                    {
                        {
                            feedAddress, feedBalance
                        }
                    },
                    Validators = Enumerable.Range(0, n).Select(j => new ValidatorInfo(
                        ecdsaPublicKeys[j], pubShares[j], feedAddress, stakeAmount
                    )).ToList()
                };
                for (var j = 0; j < n; ++j)
                {
                    genesis.Balances[addresses[j]] = "10";
                }

                var rpc = new RpcConfig
                {
                    Hosts = new[] {"+"},
                    Port = (ushort) (basePort + i),
                    MetricsPort = (ushort) (basePort + n + i),
                    ApiKey = "asdasdasd",
                };
                var walletPath = "wallet.json";
                var vault = new VaultConfig
                {
                    Path = walletPath,
                    Password = getRandomPassword()
                };
                var storage = new StorageConfig
                {
                    Path = "ChainLachain",
                    Provider = "RocksDB",
                };
                var blockchain = new BlockchainConfig
                {
                    TargetBlockTime = target,
                };
                var hardfork = new HardforkConfig
                {
                    Hardfork_1 = 123,
                };
                var version = new VersionConfig
                {
                    Version = 2
                };
                var config = new Config(net, genesis, rpc, vault, storage, blockchain, hardfork, version);
                File.WriteAllText($"config{i + 1:D2}.json", JsonConvert.SerializeObject(config, Formatting.Indented));
                GenWallet(
                    $"wallet{i + 1:D2}.json",
                    ecdsaPrivateKeys[i],
                    serializedHubPrivateKeys[i],
                    tpkeKeyGen.GetPrivKey(i).ToHex(),
                    privShares[i].ToHex(),
                    vault.Password
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

        private static void GenWallet(string path, string ecdsaKey, string hubKey, string tpkeKey, string tsKey, string password)
        {
            var config = new JsonWallet(
                ecdsaKey,
                hubKey,
                new Dictionary<ulong, string> {{0, tpkeKey}},
                new Dictionary<ulong, string> {{0, tsKey}}
            );
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            var passwordHash = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            File.WriteAllBytes(path, crypto.AesGcmEncrypt(passwordHash, json));
        }

        private static string getRandomPassword()
        {
            var crypto = CryptoProvider.GetCrypto();
            return crypto.GenerateRandomBytes(20).ToHex(false);
        }
    }
}