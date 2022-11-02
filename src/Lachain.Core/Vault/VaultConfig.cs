using System;
using Newtonsoft.Json;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

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
        
        
        public string ReadWalletPassword()
        {
            if (UseVault == true)
            {

                var vaultAddress = VaultAddress ?? 
                                   Environment.GetEnvironmentVariable("VAULT_ADDR") ??
                                   throw new ArgumentNullException(nameof(VaultAddress));
                
                var vaultToken = VaultToken ?? 
                                 Environment.GetEnvironmentVariable("VAULT_TOKEN") ??
                                 throw new ArgumentNullException(nameof(VaultToken));
                
                var vaultMountpoint = VaultMountpoint ?? 
                                      throw new ArgumentNullException(nameof(VaultMountpoint));
                
                var vaultEndpoint = VaultEndpoint ?? 
                                    throw new ArgumentNullException(nameof(VaultEndpoint));

                IAuthMethodInfo authMethod = new TokenAuthMethodInfo(vaultToken);
                VaultClientSettings vaultClientSettings = new VaultClientSettings(vaultAddress, authMethod);
                IVaultClient vaultClient = new VaultClient(vaultClientSettings);

                Secret<SecretData> secret = vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: vaultEndpoint,
                    mountPoint: vaultMountpoint
                ).Result;
                return (string) secret.Data.Data["password"];
            }
            else 
            {
                return Password ?? throw new ArgumentNullException(nameof(Password));
            }
        }
    }
}