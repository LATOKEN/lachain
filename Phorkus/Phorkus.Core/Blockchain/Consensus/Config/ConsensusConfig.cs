using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Phorkus.Core.Cryptography;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus.Config
{
    public class ConsensusConfig
    {
        public List<PublicKey> ValidatorsKeys { get; set; }
        public KeyPair KeyPair { get; set; }

        public ConsensusConfig(IConfiguration configuration/*, IWalletManager walletManager*/)
        {
            configuration
                .GetSection("consensus")
                .Bind(this/*, walletManager*/);
        }
    }
}