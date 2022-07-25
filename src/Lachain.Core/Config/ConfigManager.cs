using System;
using System.Collections.Generic;
using System.IO;
using Lachain.Core.Blockchain.Checkpoint;
using Lachain.Core.Blockchain.Hardfork;
using Lachain.Core.CLI;
using Lachain.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private const ulong _CurrentVersion = 4;
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
                        "mainnet" => 0,
                        "testnet" => 773000,
                        "devnet" => 213000,
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
                "mainnet" => 0,
                "testnet" => 900000,
                "devnet" => 355000,
                _ => 0
            };
            _config["hardfork"] = JObject.FromObject(hardforks);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 3;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }
        
        // version 4 of config should contain checkpoint initialization
        private void _UpdateConfigToV4()
        {
            var network = GetConfig<NetworkConfig>("network") ??
                          throw new ApplicationException("No network section in config");
            var checkpoints = GetConfig<CheckpointConfig>("checkpoint");
            if (checkpoints is null)
            {
                checkpoints = new CheckpointConfig
                {
                    LastCheckpoint = null,
                    AllCheckpoints = new List<CheckpointConfigInfo>()
                };
            }
            _config["checkpoint"] = JObject.FromObject(checkpoints);

            var version = GetConfig<VersionConfig>("version") ??
                          throw new ApplicationException("No version section in config");
            version.Version = 4;
            _config["version"] = JObject.FromObject(version);
            
            _SaveCurrentConfig();
        }

        private void _SaveCurrentConfig()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(_config, Formatting.Indented));
        }

        public void UpdateCheckpointConfig(List<CheckpointConfigInfo> checkpoints)
        {
            var checkpointConfig = GetConfig<CheckpointConfig>("checkpoint") ??
                throw new Exception("No checkpoint section in config");
            checkpointConfig.AllCheckpoints = checkpoints;
            foreach (var checkpoint in checkpoints)
            {
                if (checkpointConfig.LastCheckpoint is null ||
                    checkpointConfig.LastCheckpoint.BlockHeight < checkpoint.BlockHeight)
                {
                    checkpointConfig.LastCheckpoint = checkpoint;
                }
            }
            _config["checkpoint"] = JObject.FromObject(checkpointConfig);
            _SaveCurrentConfig();
        }
    }
}