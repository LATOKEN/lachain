using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Consensus.Config
{
    public class ConsensusConfig
    {
        public List<PublicKey> ValidatorsKeys { get; set; }

        public ConsensusConfig(IConfiguration configuration)
        {
            configuration
                .GetSection("network")
                .Bind(this);
        }
    }
}