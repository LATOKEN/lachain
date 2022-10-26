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

namespace Lachain.Core.Vault
{
    public class PrivateWallet : IPrivateWallet
    {
        private static readonly ILogger<PrivateWallet> Logger = LoggerFactory.GetLoggerForClass<PrivateWallet>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly ISortedDictionary<ulong, PrivateKeyShare> _tsKeys =
            new TreeDictionary<ulong, PrivateKeyShare>();
        

        private readonly string _walletPath;
        private string _walletPassword;
        private long _unlockEndTime;

        public EcdsaKeyPair EcdsaKeyPair { get; }
        public byte[] HubPrivateKey { get; }

        public PrivateWallet(IConfigManager configManager)
        {
            var config = configManager.GetConfig<VaultConfig>("vault") ??
                         throw new Exception("No 'vault' section in config file");

            if (!(configManager.CommandLineOptions.WalletPath is null))
                config.Path = configManager.CommandLineOptions.WalletPath;

            if (config.Path is null || config.Password is null)
                throw new ArgumentNullException(nameof(config));

            _walletPath = Path.IsPathRooted(config.Path) || config.Path.StartsWith("~/")
                ? config.Path
                : Path.Join(Path.GetDirectoryName(Path.GetFullPath(configManager.ConfigPath)), config.Path);

            _walletPassword = config.Password;
            _unlockEndTime = 0;
            if(!File.Exists(_walletPath))
                GenerateNewWallet(_walletPath, _walletPassword);
            var needsSave = RestoreWallet(_walletPath, _walletPassword, out var keyPair, out var hubKey);
            EcdsaKeyPair = keyPair;
            HubPrivateKey = hubKey;
            if (needsSave) SaveWallet(_walletPath, _walletPassword);
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
            var wallet = new NewJsonWallet
            (
                EcdsaKeyPair.PrivateKey.ToHex(),
                HubPrivateKey.ToHex(),
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

        private bool RestoreFromOldWallet(string path, string password, out EcdsaKeyPair keyPair, out byte[] hubKey)
        {
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var decryptedContent =
                Encoding.UTF8.GetString(Crypto.AesGcmDecrypt(key, encryptedContent));

            var wallet = JsonConvert.DeserializeObject<OldJsonWallet>(decryptedContent);
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
            _tsKeys.AddAll(wallet.ThresholdSignatureKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKeyShare>(p.Key,
                        PrivateKeyShare.FromBytes(p.Value.HexToBytes()))));
            return needsSave;
        }

        private bool RestoreFromNewWallet(string path, string password, out EcdsaKeyPair keyPair, out byte[] hubKey)
        {
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var decryptedContent =
                Encoding.UTF8.GetString(Crypto.AesGcmDecrypt(key, encryptedContent));

            var wallet = JsonConvert.DeserializeObject<NewJsonWallet>(decryptedContent);
            if (wallet.EcdsaPrivateKey is null)
                throw new Exception("Decrypted wallet does not contain ECDSA key");
            var needsSave = false;
            if (wallet.HubPrivateKey is null)
            {
                wallet.HubPrivateKey = GenerateHubKey().PrivateKey;
                needsSave = true;
            }

            wallet.ThresholdSignatureKeys ??= new Dictionary<ulong, string>();

            keyPair = new EcdsaKeyPair(wallet.EcdsaPrivateKey.HexToBytes().ToPrivateKey());
            hubKey = wallet.HubPrivateKey.HexToBytes();
            _tsKeys.AddAll(wallet.ThresholdSignatureKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKeyShare>(p.Key,
                        PrivateKeyShare.FromBytes(p.Value.HexToBytes()))));
            return needsSave;
        }

        private bool RestoreWallet(string path, string password, out EcdsaKeyPair keyPair, out byte[] hubKey)
        {
            try
            {
                var needsSave = RestoreFromNewWallet(path, password, out var key, out var hubPrivateKey);
                keyPair = key;
                hubKey = hubPrivateKey;
                return needsSave;
            }
            catch (Exception ex)
            {
                Logger.LogInformation($"Could not restore wallet from new configuration, trying old configuration: {ex}");
                try
                {
                    var needsSave = RestoreFromOldWallet(path, password, out var key, out var hubPrivateKey);
                    keyPair = key;
                    hubKey = hubPrivateKey;
                    return needsSave;
                }
                catch (Exception ex1)
                {
                    Logger.LogError($"Could not restore wallet from old or new configuration. Wallet is corrupted: {ex1}");
                    throw;
                }
            }
        }
        
        private static void GenerateNewWallet(string path, string password)
        {
            var config = new NewJsonWallet(
                CryptoProvider.GetCrypto().GenerateRandomBytes(32).ToHex(false),
                GenerateHubKey().PrivateKey,
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
    }
}