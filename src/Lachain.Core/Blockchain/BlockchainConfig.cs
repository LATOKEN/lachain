using Newtonsoft.Json;

namespace Lachain.Core.Blockchain
{
    public class BlockchainConfig
    {
        // targetBlockTime in miliseconds
        [JsonProperty("targetBlockTime")] public ulong TargetBlockTime;
        [JsonProperty("targetTransactionsPerBlock")] public int TargetTransactionsPerBlock;
    }
}