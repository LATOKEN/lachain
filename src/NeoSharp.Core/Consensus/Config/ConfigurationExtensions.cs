using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NeoSharp.Core.Cryptography;
using NeoSharp.Types.ExtensionMethods;

namespace NeoSharp.Core.Consensus.Config
{
    public static class ConfigurationExtensions
    {
        public static void Bind(this IConfiguration config, ConsensusConfig consensusConfig)
        {
            consensusConfig.ValidatorsKeys = new List<PublicKey>();
            for (var i = 0;; ++i)
            {
                var rawKey = ParseString(config.GetSection("standbyValidators"), i.ToString());
                if (rawKey == null) break;
                consensusConfig.ValidatorsKeys.Add(new PublicKey(rawKey.HexToBytes()));
            }
        }

        private static string ParseString(IConfiguration config, string section)
        {
            return config.GetSection(section)?.Get<string>();
        }
    }
}