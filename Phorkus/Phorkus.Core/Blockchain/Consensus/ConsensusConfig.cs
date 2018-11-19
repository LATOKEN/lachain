using System.Collections.Generic;
using Newtonsoft.Json;
using Phorkus.Core.Proto;

namespace Phorkus.Core.Blockchain.Consensus
{
    public class ConsensusConfig
    {
        [JsonProperty("validators")]
        public List<PublicKey> ValidatorsKeys { get; set; }

        [JsonProperty("privateKey")]
        public string PrivateKey { get; set; }
    }
}