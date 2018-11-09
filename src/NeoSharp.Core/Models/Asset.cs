using NeoSharp.BinarySerialization;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    public class Asset
    {
        [BinaryProperty(1)]
        [JsonProperty("hash")]
        public UInt256 Hash;
        
        [BinaryProperty(2)]
        [JsonProperty("type")]
        public AssetType AssetType;
        
        [BinaryProperty(3)]
        [JsonProperty("name")]
        public string Name;

        [BinaryProperty(4)]
        [JsonProperty("amount")]
        public UInt256 Amount;

        [BinaryProperty(5)]
        [JsonProperty("precision")]
        public byte Precision;
        
        [BinaryProperty(6)]
        [JsonProperty("admin")]
        public UInt160 Owner;

    }
}
