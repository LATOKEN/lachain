using Newtonsoft.Json;

namespace Lachain.Core.Blockchain
{
    public class BlockchainConfig
    {
        [JsonProperty("targetBlockTime")] public ulong TargetBlockTime;
        [JsonProperty("chainId")] public int ChainId;   
    }
}