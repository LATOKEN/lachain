using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using C5;
using Lachain.Core.Config;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
using Lachain.Logger;
using Lachain.Utility.Serialization;
using Lachain.Utility.Utils;
using Newtonsoft.Json;
using PublicKey = Lachain.Crypto.TPKE.PublicKey;
using VaultSharp;
using VaultSharp.V1.AuthMethods;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;

namespace Lachain.Core.Vault
{
    public class PrivateWallet : IPrivateWallet
    {
        private static readonly ILogger<PrivateWallet> Logger = LoggerFactory.GetLoggerForClass<PrivateWallet>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly ISortedDictionary<ulong, PrivateKeyShare> _tsKeys =
            new TreeDictionary<ulong, PrivateKeyShare>();
        
        private readonly ISortedDictionary<ulong, PrivateKey> _tpkeKeys = new TreeDictionary<ulong, PrivateKey>();

        private readonly string _walletPath;
        private string _walletPassword;
        private long _unlockEndTime;
        private VaultConfig vaultConfig;

        public EcdsaKeyPair EcdsaKeyPair { get; }
        public byte[] HubPrivateKey { get; }

        public PrivateWallet(IConfigManager configManager)
        {
            vaultConfig = configManager.GetConfig<VaultConfig>("vault") ??
                         throw new Exception("No 'vault' section in config file");

            _walletPath = ReadWalletPath(configManager);
            _walletPassword = ReadWalletPassword();
            
            _unlockEndTime = 0;
            if(!File.Exists(_walletPath))
                GenerateNewWallet(_walletPath, _walletPassword);
            var needsSave = RestoreWallet(_walletPath, _walletPassword, out var keyPair, out var hubKey);
            EcdsaKeyPair = keyPair;
            HubPrivateKey = hubKey;
            if (needsSave) SaveWallet(_walletPath, _walletPassword);
        }

        public PrivateKey? GetTpkePrivateKeyForBlock(ulong block)
        {
            try
            {
                return _tpkeKeys.Predecessor(block + 1).Value;
            }
            catch (NoSuchItemException)
            {
                return null;
            }
        }

        public void AddTpkePrivateKeyAfterBlock(ulong block, PrivateKey key)
        {
            if (_tpkeKeys.Contains(block))
            {
                _tpkeKeys.Update(block, key);
                Logger.LogWarning($"TpkePrivateKey for block {block} is overwritten");
            }
            else
            {
                _tpkeKeys.Add(block, key);
            }

            SaveWallet(_walletPath, _walletPassword);
        }

        public PrivateKeyShare? GetThresholdSignatureKeyForBlock(ulong block)
        {
            return !_tsKeys.TryPredecessor(block + 1, out var predecessor) ? null : predecessor.Value;
        }

        public void AddThresholdSignatureKeyAfterBlock(ulong block, PrivateKeyShare key)
        {
            if (_tsKeys.Contains(block))
            {
                _tsKeys.Update(block, key);
                Logger.LogWarning($"ThresholdSignatureKey for block {block} is overwritten");
            }
            else
            {
                _tsKeys.Add(block, key);
            }

            SaveWallet(_walletPath, _walletPassword);
        }

        private void SaveWallet(string path, string password)
        {
            var wallet = new JsonWallet
            (
                EcdsaKeyPair.PrivateKey.ToHex(),
                HubPrivateKey.ToHex(),
                new Dictionary<ulong, string>(
                    _tpkeKeys.Select(p =>
                        new System.Collections.Generic.KeyValuePair<ulong, string>(
                            p.Key, p.Value.ToHex()
                        )
                    )
                ),
                new Dictionary<ulong, string>(
                    _tsKeys.Select(p =>
                        new System.Collections.Generic.KeyValuePair<ulong, string>(
                            p.Key, p.Value.ToHex()
                        )
                    )
                )
            );
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(wallet));
            var key = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var encryptedContent = Crypto.AesGcmEncrypt(key, json);
            File.WriteAllBytes(path, encryptedContent);
        }

        public static (string PrivateKey, string PublicKey) GenerateHubKey()
        {
            var keyInfo = CommunicationHub.Net.Hub.GenerateNewHubKey().Split(",");
            if (keyInfo.Length != 2)
                throw new Exception("Invalid hub key");
            return (keyInfo[0], keyInfo[1]);
        }

        private bool RestoreWallet(string path, string password, out EcdsaKeyPair keyPair, out byte[] hubKey)
        {
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var decryptedContent =
                Encoding.UTF8.GetString(Crypto.AesGcmDecrypt(key, encryptedContent));

            var wallet = JsonConvert.DeserializeObject<JsonWallet>(decryptedContent);
            if (wallet.EcdsaPrivateKey is null)
                throw new Exception("Decrypted wallet does not contain ECDSA key");
            var needsSave = false;
            if (wallet.HubPrivateKey is null)
            {
                wallet.HubPrivateKey = GenerateHubKey().PrivateKey;
                needsSave = true;
            }

            wallet.ThresholdSignatureKeys ??= new Dictionary<ulong, string>();
            wallet.TpkePrivateKeys ??= new Dictionary<ulong, string>();

            keyPair = new EcdsaKeyPair(wallet.EcdsaPrivateKey.HexToBytes().ToPrivateKey());
            hubKey = wallet.HubPrivateKey.HexToBytes();
            _tpkeKeys.AddAll(wallet.TpkePrivateKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKey>(p.Key, PrivateKey.FromBytes(p.Value.HexToBytes()))));
            _tsKeys.AddAll(wallet.ThresholdSignatureKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKeyShare>(p.Key,
                        PrivateKeyShare.FromBytes(p.Value.HexToBytes()))));
            return needsSave;
        }
        
        private static void GenerateNewWallet(string path, string password)
        {
            var config = new JsonWallet(
                CryptoProvider.GetCrypto().GenerateRandomBytes(32).ToHex(false),
                GenerateHubKey().PrivateKey,
                new Dictionary<ulong, string> {},
                new Dictionary<ulong, string> {}
            );
            var json = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(config));
            var passwordHash = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var crypto = CryptoProvider.GetCrypto();
            File.WriteAllBytes(path, crypto.AesGcmEncrypt(passwordHash, json));
        }

        public bool HasKeyForKeySet(PublicKeySet thresholdSignaturePublicKeySet, ulong beforeBlock)
        {
            try
            {
                return thresholdSignaturePublicKeySet.Keys.Contains(_tsKeys.Predecessor(beforeBlock).Value
                    .GetPublicKeyShare());
            }
            catch (NoSuchItemException)
            {
                return false;
            }
        }

        public void DeleteKeysAfterBlock(ulong block)
        {
            _tsKeys.RemoveRangeFrom(block + 1);
            _tpkeKeys.RemoveRangeFrom(block + 1);
            SaveWallet(_walletPath, _walletPassword);
        }

        public bool Unlock(string password, long s)
        {
            if (password == _walletPassword)
            {
                var now = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
                _unlockEndTime = now + s;
                return true;
            }

            return false;
        }

        public bool IsLocked()
        {
            return new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds() > _unlockEndTime;
        }

        public IPrivateWallet? GetWalletInstance()
        {
            var now = new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
            return now < _unlockEndTime ? this : null;
        }
        
        public bool ChangePassword(string currentPassword, string newPassword)
        {
            if (currentPassword != _walletPassword) return false;
            SaveWallet(_walletPath, newPassword);
            _walletPassword = newPassword;

            return true;
        }

        private string ReadWalletPassword()
        {
            if (vaultConfig.UseVault == true)
            {
                if (vaultConfig.VaultToken is null || vaultConfig.VaultPath is null)
                    throw new ArgumentNullException(nameof(vaultConfig));

                IAuthMethodInfo authMethod = new TokenAuthMethodInfo(vaultToken: vaultConfig.VaultToken);
                VaultClientSettings vaultClientSettings = new VaultClientSettings(vaultConfig.VaultPath, authMethod);
                IVaultClient vaultClient = new VaultClient(vaultClientSettings);

                Secret<SecretData> secret = vaultClient.V1.Secrets.KeyValue.V2.ReadSecretAsync(
                    path: "/wallet-data",
                    mountPoint: "secret"
                ).Result;
                
                return (string) secret.Data.Data["password"];
                
            }
            else 
            {
                return vaultConfig.Password ?? throw new ArgumentNullException(nameof(vaultConfig.Password));
            }
        }

        private string ReadWalletPath(IConfigManager configManager)
        {
            if (!(configManager.CommandLineOptions.WalletPath is null))
                vaultConfig.Path = configManager.CommandLineOptions.WalletPath;
            if (vaultConfig.Path is null)
                throw new ArgumentNullException(nameof(vaultConfig.Path));

            return Path.IsPathRooted(vaultConfig.Path) || vaultConfig.Path.StartsWith("~/")
                ? vaultConfig.Path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(configManager.ConfigPath)), vaultConfig.Path);
        }
    }
}