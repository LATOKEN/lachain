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

namespace Lachain.Core.Vault
{
    class PrivateWallet : IPrivateWallet
    {
        private static readonly ILogger<PrivateWallet> Logger = LoggerFactory.GetLoggerForClass<PrivateWallet>();

        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly ISortedDictionary<ulong, PrivateKeyShare> _tsKeys =
            new TreeDictionary<ulong, PrivateKeyShare>();

        private readonly ISortedDictionary<ulong, PrivateKey> _tpkeKeys = new TreeDictionary<ulong, PrivateKey>();

        private readonly string _walletPath;
        private readonly string _walletPassword;
        private long _unlockEndTime;

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
            RestoreWallet(_walletPath, _walletPassword, out var keyPair);
            EcdsaKeyPair = keyPair;
        }

        public EcdsaKeyPair EcdsaKeyPair { get; }

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
            _tpkeKeys.Add(block, key);
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

        private void RestoreWallet(string path, string password, out EcdsaKeyPair keyPair)
        {
            var encryptedContent = File.ReadAllBytes(path);
            var key = Encoding.UTF8.GetBytes(password).KeccakBytes();
            var decryptedContent =
                Encoding.UTF8.GetString(Crypto.AesGcmDecrypt(key, encryptedContent));

            var wallet = JsonConvert.DeserializeObject<JsonWallet>(decryptedContent);
            if (wallet.EcdsaPrivateKey is null)
                throw new Exception("Decrypted wallet does not contain ECDSA key");
            wallet.ThresholdSignatureKeys ??= new Dictionary<ulong, string>();
            wallet.TpkePrivateKeys ??= new Dictionary<ulong, string>();

            keyPair = new EcdsaKeyPair(wallet.EcdsaPrivateKey.HexToBytes().ToPrivateKey());
            _tpkeKeys.AddAll(wallet.TpkePrivateKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKey>(p.Key, PrivateKey.FromBytes(p.Value.HexToBytes()))));
            _tsKeys.AddAll(wallet.ThresholdSignatureKeys
                .Select(p =>
                    new C5.KeyValuePair<ulong, PrivateKeyShare>(p.Key,
                        PrivateKeyShare.FromBytes(p.Value.HexToBytes()))));
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
    }
}