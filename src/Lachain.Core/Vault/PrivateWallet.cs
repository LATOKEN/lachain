#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Text;
using C5;
using Lachain.Core.Config;
using Lachain.Crypto;
using Lachain.Crypto.ECDSA;
using Lachain.Crypto.ThresholdSignature;
using Lachain.Crypto.TPKE;
using Lachain.Utility.Utils;
using Newtonsoft.Json;

namespace Lachain.Core.Vault
{
    class PrivateWallet : IPrivateWallet
    {
        private static readonly ICrypto Crypto = CryptoProvider.GetCrypto();

        private readonly ISortedDictionary<ulong, PrivateKeyShare> _tsKeys =
            new TreeDictionary<ulong, PrivateKeyShare>();

        private readonly ISortedDictionary<ulong, PrivateKey> _tpkeKeys = new TreeDictionary<ulong, PrivateKey>();

        private readonly string _walletPath;

        public PrivateWallet(IConfigManager configManager)
        {
            var config = configManager.GetConfig<VaultConfig>("vault");
            if (config?.Path is null || config.Password is null)
                throw new ArgumentNullException(nameof(config));
            _walletPath = config.Path;
            var encryptedContent = File.ReadAllBytes(config.Path);
            var key = Encoding.UTF8.GetBytes(config.Password).KeccakBytes();
            var decryptedContent =
                Encoding.UTF8.GetString(Crypto.AesGcmDecrypt(key, encryptedContent));

            var wallet = JsonConvert.DeserializeObject<JsonWallet>(decryptedContent);

            EcdsaKeyPair = new EcdsaKeyPair(wallet.EcdsaPrivateKey.HexToBytes().ToPrivateKey());
            _tpkeKeys.AddAll(wallet.TpkePrivateKeys
                .Select(p => new KeyValuePair<ulong, PrivateKey>(p.Key, PrivateKey.FromBytes(p.Value.HexToBytes()))));
            _tsKeys.AddAll(wallet.ThresholdSignatureKeys
                .Select(p =>
                    new KeyValuePair<ulong, PrivateKeyShare>(p.Key, PrivateKeyShare.FromBytes(p.Value.HexToBytes()))));
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
        }

        public PrivateKeyShare? GetThresholdSignatureKeyForBlock(ulong block)
        {
            try
            {
                return _tsKeys.Predecessor(block + 1).Value;
            }
            catch (NoSuchItemException)
            {
                return null;
            }
        }

        public void AddThresholdSignatureKeyAfterBlock(ulong block, PrivateKeyShare key)
        {
            _tsKeys.Add(block, key);
        }
    }
}