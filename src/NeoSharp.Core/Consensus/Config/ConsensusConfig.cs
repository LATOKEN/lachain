using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Wallet;

namespace NeoSharp.Core.Consensus.Config
{
    public class ConsensusConfig
    {
        public List<PublicKey> ValidatorsKeys { get; set; }
        public KeyPair KeyPair { get; set; }

        public ConsensusConfig(IConfiguration configuration, IWalletManager walletManager)
        {
            configuration
                .GetSection("consensus")
                .Bind(this, walletManager);
        }
    }
}