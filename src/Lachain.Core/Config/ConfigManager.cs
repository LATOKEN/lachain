using System;
using System.Collections.Generic;
using System.IO;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.CLI;
using Lachain.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private const ulong _CurrentVersion = 12;
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
                "mainnet" => 3970000,
                "testnet" => 3612000,
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

        private void _SaveCurrentConfig()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
    }
}
