using Newtonsoft.Json;

namespace Lachain.Core.Config
{
    public class VersionConfig
    {
        [JsonProperty("configVersion")] public ulong Version;
    }
}

