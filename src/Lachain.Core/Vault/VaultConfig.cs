using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    public class VaultConfig
    {
        [JsonProperty("path")] public string? Path { get; set; }
        [JsonProperty("password")] public string? Password { get; set; }
    }
}