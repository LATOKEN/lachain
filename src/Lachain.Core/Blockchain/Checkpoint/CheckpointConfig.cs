using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lachain.Core.Blockchain.Checkpoint
{
    public class CheckpointConfig
    {
        [JsonProperty("lastCheckpoint")] public CheckpointConfigInfo? LastCheckpoint { get; set; }
        [JsonProperty("allCheckpoints")] public List<CheckpointConfigInfo>? AllCheckpoints { get; set; }
    }
}