using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lachain.Core.Blockchain.Checkpoints
{
    public class CheckpointConfigInfo
    {
        [JsonProperty("blockHeight")] public ulong BlockHeight { get; set; }
        [JsonProperty("blockHash")] public string? BlockHash { get; set; }

        [JsonProperty("stateHashes")]
        public IDictionary<string, string> StateHashes { get; set; } = new Dictionary<string, string>();
        
        public CheckpointConfigInfo(ulong blockHeight)
        {
            BlockHeight = blockHeight;
        }
        [JsonConstructor]
        public CheckpointConfigInfo(ulong blockHeight, string? blockHash, IDictionary<string, string> stateHashes)
        {
            BlockHeight = blockHeight;
            BlockHash = blockHash;
            StateHashes = stateHashes;
        }
    }
}