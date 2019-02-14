using System.Collections.Generic;
using Newtonsoft.Json;

namespace Phorkus.Core.Blockchain.Genesis
{
    public class GenesisConfig
    {
        [JsonProperty("balances")]
        public Dictionary<string, string> Balances { get; set; } = new Dictionary<string, string>();

        [JsonProperty("privateKey")]
        public string PrivateKey { get; set; }
    }
}