using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Lachain.Core.Config
{
    public class LocalConfigLoader : IConfigLoader
    {
        private readonly string _filePath;

        internal LocalConfigLoader(string filePath)
        {
            _filePath = filePath;
        }

        public IReadOnlyDictionary<string, object> LoadConfig()
        {
            var body = File.ReadAllText(_filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(body);
        }
    }
}