using System.Collections.Generic;
using Lachain.Core.CLI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private readonly IReadOnlyDictionary<string, object> _config;
        public string ConfigPath { get; }
        public RunOptions CommandLineOptions { get; }

        public ConfigManager(string filePath, RunOptions options)
        {
            CommandLineOptions = options;
            var configLoader = new LocalConfigLoader(filePath);
            _config = configLoader.LoadConfig();
            ConfigPath = filePath;
        }

        public T? GetConfig<T>(string name)
            where T : class
        {
            return !(_config[name] is JObject props) ? default : JsonConvert.DeserializeObject<T>(props.ToString());
        }
    }
}