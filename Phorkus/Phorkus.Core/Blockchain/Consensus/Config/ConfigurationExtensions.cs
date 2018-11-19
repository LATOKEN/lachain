using System.Security;
using Microsoft.Extensions.Configuration;

namespace Phorkus.Core.Blockchain.Consensus.Config
{
    public static class ConfigurationExtensions
    {
        public static void Bind(
            this IConfiguration config, ConsensusConfig consensusConfig
        )
        {
//            consensusConfig.ValidatorsKeys = new List<PublicKey>();
//            for (var i = 0;; ++i)
//            {
//                var rawKey = ParseString(config.GetSection("standbyValidators"), i.ToString());
//                if (rawKey == null) break;
//                consensusConfig.ValidatorsKeys.Add(new PublicKey(rawKey.HexToBytes()));
//            }
//
//            var walletFile = ParseString(config, "wallet");
//            var walletPassword = ParseSecureString(config, "password");
//            if (walletFile != null)
//            {
//                walletManager.Load(walletFile);
//                var nep2 = walletManager.Wallet.Accounts[0].Key;
//                consensusConfig.KeyPair = new KeyPair(walletManager.DecryptNep2(nep2, walletPassword));
//            }
        }

        private static string ParseString(IConfiguration config, string section)
        {
//            return config.GetSection(section)?.Get<string>();
            return null;
        }
        
        private static SecureString ParseSecureString(IConfiguration config, string section)
        {
//            string value = config.GetSection(section)?.Get<string>();
//            if (value == null) return null;
//            SecureString result = new SecureString();
//            foreach (var c in value) result.AppendChar(c);
//            result.MakeReadOnly();
//            return result;
            return null;
        }
    }
}