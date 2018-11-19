using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phorkus.Core.Config
{
    public class ConfigManager : IConfigManager
    {
        private readonly IReadOnlyDictionary<string, object> _config;
        
        public ConfigManager(string filePath)
        {
            var configLoader = new LocalConfigLoader(filePath);
            _config = configLoader.LoadConfig();
        }
        
        public T GetConfig<T>(string name)
            where T : new()
        {
            return !(_config[name] is JObject props) ? default(T) : JsonConvert.DeserializeObject<T>(props.ToString());
        }
    }
}