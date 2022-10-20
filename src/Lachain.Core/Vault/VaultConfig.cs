using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    public class VaultConfig
    {
        [JsonProperty("useVault")] public bool? UseVault { get; set; }
        [JsonProperty("vaultPath")] public string? VaultPath { get; set; }
        [JsonProperty("vaultToken")] public string? VaultToken { get; set; }
        [JsonProperty("path")] public string? Path { get; set; }
        [JsonProperty("password")] public string? Password { get; set; }
    }
}