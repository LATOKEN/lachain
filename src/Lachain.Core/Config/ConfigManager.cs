using System;
using System.Collections.Generic;
using System.IO;
using Lachain.Core.Blockchain;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.CLI;
using Lachain.Core.Vault;
using Lachain.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private const ulong _CurrentVersion = 19;
        private IDictionary<string, object> _config;
        public string ConfigPath { get; }
        public RunOptions CommandLineOptions { get; }
        public ConfigManager(string filePath, RunOptions options)
        {
            CommandLineOptions = options;
            var configLoader = new LocalConfigLoader(filePath);
            _config = new Dictionary<string, object>(configLoader.LoadConfig());
            ConfigPath = filePath;
            _UpdateConfigVersion();
        }
        
        public void UpdateWalletPassword(string password)
        {
            var vault = GetConfig<VaultConfig>("vault") ??
                        throw new ApplicationException("No vault section in config");

            if (vault.UseVault == true)
                throw new ApplicationException("Vault is being used. Password cannot be written to config");

            vault.Password = password;
            _config["vault"] = JObject.FromObject(vault);
            _SaveCurrentConfig();
        }

        public T? GetConfig<T>(string name)
            where T : class
        {
            if (!_config.ContainsKey(name))
                return default;
            return !(_config[name] is JObject props) ? default : JsonConvert.DeserializeObject<T>(props.ToString());
        }

        private void _UpdateConfigVersion()
        {
            ulong version = 1;
            version = GetConfig<VersionConfig>("versionInfo")?.Version ?? 1;
            if (version > _CurrentVersion)
                throw new ApplicationException("Unknown config version");
            if (version == _CurrentVersion)
                return;
            if (version < 2)
                _UpdateConfigToV2();
            if (version < 3)
                _UpdateConfigToV3();
            if (version < 4)
                _UpdateConfigToV4();
            if (version < 5)
                _UpdateConfigToV5();
            if (version < 6)
                _UpdateConfigToV6();
            if (version < 7)
                _UpdateConfigToV7();
            if (version < 8)
                _UpdateConfigToV8();
            if (version < 9)
                _UpdateConfigToV9();
            if (version < 10)
                _UpdateConfigToV10();
            if (version < 11)
                _UpdateConfigToV11();
            if (version < 12)
                _UpdateConfigToV12();
            if (version < 13)
                _UpdateConfigToV13();
            if (version < 14)
                _UpdateConfigToV14();
            if (version < 15)
                _UpdateConfigToV15();
            if (version < 16)
                _UpdateConfigToV16();
            if (version < 17)
                _UpdateConfigToV17();
            if (version < 18)
                _UpdateConfigToV18();
            if (version < 19)
                _UpdateConfigToV19();
            version = GetConfig<VersionConfig>("version")?.Version ??
                throw new ApplicationException("No version section in config");
            if (version != _CurrentVersion)
                throw new ApplicationException("Version not updated properly");
        }

        // version 2 of config should contain hardfork section and height for first hardfork,
        // cycle duration and validatorCount in config
        private void _UpdateConfigToV2()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            
            network.ValidatorsCount ??= network.NetworkName switch
            {
                "mainnet" => 7,
                "testnet" => 4,
                "devnet" => 22,
                _ => 4
            };

            network.CycleDuration ??= network.NetworkName switch
            {
                "mainnet" => 1000,
                "testnet" => 1000,
                "devnet" => 40,
                _ => 40
            };

            _config["network"] = JObject.FromObject(network);

            var hardforks = GetConfig<HardforkConfig>("hardfork");
            if (hardforks is null)
            {
                hardforks = new HardforkConfig
                {
                    Hardfork_1 = network.NetworkName switch
                    {
                        "mainnet" => 1525000,
                        "testnet" => 773000,
                        "devnet" => 0,
                        _ => 0
                    }
                };
                _config["hardfork"] = JObject.FromObject(hardforks);
            }

            var version = GetConfig<VersionConfig>("version");
            if (version is null)
            {
                version = new VersionConfig { Version = 2 };
                _config["version"] = JObject.FromObject(version);
            }
            
            _SaveCurrentConfig();
        }

        // version 3 of config should contain hardfork height for second hardfork,
        private void _UpdateConfigToV3()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_2 ??= network.NetworkName switch
            {
                "mainnet" => 1525000,
                "testnet" => 900000,
                "devnet" => 0,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 3;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 4 of config should contain hardfork height for hardfork_3,
        private void _UpdateConfigToV4()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_3 ??= network.NetworkName switch
            {
                "mainnet" => 1525000,
                "testnet" => 1220000,
                "devnet" => 0,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 4;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 5 of config should contain cache option
        private void _UpdateConfigToV5()
        {
            var cacheOption = new CacheOptions();
            cacheOption.SizeLimit ??= 100; // 100 is default blockSizeLimit
            var cache = GetConfig<CacheConfig>("cache") ?? new CacheConfig();
            cache.BlockHeight = cacheOption;
            _config["cache"] = JObject.FromObject(cache);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 5;
            _config["version"] = JObject.FromObject(version);

            _SaveCurrentConfig();
        }

        // version 6 of config should contain hardfork height for hardfork_4,
        private void _UpdateConfigToV6()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_4 ??= network.NetworkName switch
            {
                "mainnet" => 2250000,
                "testnet" => 1904000,
                "devnet" => 0,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 6;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 7 of config should contain hardfork height for hardfork_5,
        private void _UpdateConfigToV7()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_5 ??= network.NetworkName switch
            {
                "mainnet" => 2550000,
                "testnet" => 2239000,
                "devnet" => 27500,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 7;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 8 of config should contain hardfork height for hardfork_6
        private void _UpdateConfigToV8()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            
            network.NewChainId ??= network.NetworkName switch
            {
                "mainnet" => 225,
                "testnet" => 226,
                "devnet" => 227,
                _ => 42
            };
            _config["network"] = JObject.FromObject(network);

            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_6 ??= network.NetworkName switch
            {
                "mainnet" => 2780000,
                "testnet" => 2543000,
                "devnet" => 145700,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 8;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 9 of config should contain hardfork height for hardfork_7
        private void _UpdateConfigToV9()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_7 ??= network.NetworkName switch
            {
                "mainnet" => 2840000,
                "testnet" => 2615000,
                "devnet" => 214000,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 9;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 10 of config should contain hardfork height for hardfork_8
        private void _UpdateConfigToV10()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_8 ??= network.NetworkName switch
            {
                "mainnet" => 3205000,
                "testnet" => 3000000,
                "devnet" => 350000,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 10;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 11 of config should contain hardfork height for hardfork_9
        private void _UpdateConfigToV11()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_9 ??= network.NetworkName switch
            {
                "mainnet" => 3709350,
                "testnet" => 3095300,
                "devnet" => 743530,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 11;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 12 of config should contain hardfork height for hardfork_10
        private void _UpdateConfigToV12()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_10 ??= network.NetworkName switch
            {
                "mainnet" => 3985000,
                "testnet" => 3628500,
                "devnet" => 762700,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 12;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 13 of config should contain hardfork height for hardfork_11
        private void _UpdateConfigToV13()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_11 ??= network.NetworkName switch
            {
                "mainnet" => 5251000,
                "testnet" => 4933000,
                "devnet" => 1019300,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 13;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 14 of config should contain hardfork height for hardfork_12
        private void _UpdateConfigToV14()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_12 ??= network.NetworkName switch
            {
                "mainnet" => 5405300,
                "testnet" => 5073300,
                "devnet" => 1270300,
                _ => 10
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 14;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 15 of config should contain hardfork height for hardfork_13
        private void _UpdateConfigToV15()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_13 ??= network.NetworkName switch
            {
                "mainnet" => 5935300,
                "testnet" => 5625300,
                "devnet" => 1525000,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 15;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        // version 16 of config should contain updated blockchain.TargetBlockTime (4000 ms) 
        // and new blockchain.TargetTransactionsPerBlock (800) 
        private void _UpdateConfigToV16()
        {   
            const int oldTargetBlockTime = 5000; //ms
            const int newTargetBlockTime = 4000; //ms
            const int newTargetTransactionsPerBlock = 800; 

            var blockchain = GetConfig<BlockchainConfig>("blockchain") ??
                            throw new ApplicationException("No blockchain section in config");

            if (blockchain.TargetBlockTime == oldTargetBlockTime)
                blockchain.TargetBlockTime = newTargetBlockTime;

            blockchain.TargetTransactionsPerBlock = newTargetTransactionsPerBlock;
            _config["blockchain"] = JObject.FromObject(blockchain);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            
            version.Version = 16;
            _config["version"] = JObject.FromObject(version);

            _SaveCurrentConfig();
        }
        
        // version 17 of config should contain minimal validators count 
        private void _UpdateConfigToV17()
        {   
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            network.MinValidatorsCount ??= network.NetworkName switch
            {
                "mainnet" => 4,
                "testnet" => 4,
                "devnet" => 4,
                _ => 1
            };
            _config["network"] = JObject.FromObject(network);
            
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_14 ??= network.NetworkName switch
            {
                "mainnet" => 6211300,
                "testnet" => 5927300,
                "devnet" => 1650300,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            
            version.Version = 17;
            _config["version"] = JObject.FromObject(version);

            _SaveCurrentConfig();
        }

        // version 18 of config should contain hardfork_15
        private void _UpdateConfigToV18()
        {   
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_15 ??= network.NetworkName switch
            {
                "mainnet" => 7887120,
                "testnet" => 7490462,
                "devnet" => 7887120,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            
            version.Version = 18;
            _config["version"] = JObject.FromObject(version);

            _SaveCurrentConfig();
        }

        // version 19 of config should contain hardfork_16
        private void _UpdateConfigToV19()
        {   
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            
            var hardforks = GetConfig<HardforkConfig>("hardfork") ??
                            throw new ApplicationException("No hardfork section in config");
            hardforks.Hardfork_16 ??= network.NetworkName switch
            {
                "mainnet" => 8028800,
                "testnet" => 7751300,
                "devnet" => 7887130,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            
            version.Version = 19;
            _config["version"] = JObject.FromObject(version);

            _SaveCurrentConfig();
        }
        
        private void _SaveCurrentConfig()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
    }
}
