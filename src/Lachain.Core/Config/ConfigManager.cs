using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Lachain.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private readonly IReadOnlyDictionary<string, object> _config;

        public ConfigManager(string filePath)
        {
            var configLoader = new LocalConfigLoader(filePath);
            _config = configLoader.LoadConfig();
        }

        public T? GetConfig<T>(string name)
            where T : class
        {
            return !(_config[name] is JObject props) ? default : JsonConvert.DeserializeObject<T>(props.ToString());
        }
    }
}