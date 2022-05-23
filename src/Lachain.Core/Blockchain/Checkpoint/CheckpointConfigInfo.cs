using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lachain.Core.Blockchain.Checkpoint
{
    public class CheckpointConfigInfo
    {
        [JsonProperty("blockHeight")] public ulong BlockHeight;
        [JsonProperty("blockHash")] public string? BlockHash;
        [JsonProperty("stateHashes")] public IDictionary<string, string> StateHashes = new Dictionary<string, string>();

        public CheckpointConfigInfo(ulong blockHeight)
        {
            BlockHeight = blockHeight;
        }
        public CheckpointConfigInfo(ulong blockHeight, string blockHash, Dictionary<string, string> stateHashes)
        {
            BlockHeight = blockHeight;
            BlockHash = blockHash;
            StateHashes = stateHashes;
        }
    }
}