using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private readonly IReadOnlyDictionary<string, object> _config;
        private readonly Func<string, string?, string?> _argGetter;
        public string ConfigPath { get; }

        public ConfigManager(string filePath, Func<string, string?, string?> argGetter)
        {
            var configLoader = new LocalConfigLoader(filePath);
            _config = configLoader.LoadConfig();
            ConfigPath = filePath;
            _argGetter = argGetter;
        }

        public T? GetConfig<T>(string name)
            where T : class
        {
            return !(_config[name] is JObject props) ? default : JsonConvert.DeserializeObject<T>(props.ToString());
        }

        public string? GetCliArg(string name)
        {
            return _argGetter?.Invoke(name, null);
        }
    }
}