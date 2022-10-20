using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    public class VaultConfig
    {
        [JsonProperty("useVault")] public bool? UseVault { get; set; }
        [JsonProperty("vaultAddress")] public string? VaultAddress { get; set; }
        [JsonProperty("vaultToken")] public string? VaultToken { get; set; }
        [JsonProperty("vaultMountpoint")] public string? VaultMountpoint { get; set; }
        [JsonProperty("vaultEndpoint")] public string? VaultEndpoint { get; set; }
        [JsonProperty("path")] public string? Path { get; set; }
        [JsonProperty("password")] public string? Password { get; set; }
    }
}