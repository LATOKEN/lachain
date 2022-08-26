using Newtonsoft.Json;
using System.Collections.Generic;

namespace Lachain.Core.Blockchain.Checkpoints
{
    public class CheckpointConfig
    {
        [JsonProperty("lastCheckpoint")] public CheckpointConfigInfo? LastCheckpoint { get; set; }
        [JsonProperty("allCheckpoints")] public List<CheckpointConfigInfo> AllCheckpoints { get; set; }
            = new List<CheckpointConfigInfo>();
    }
}