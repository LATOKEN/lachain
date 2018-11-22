using NeoSharp.BinarySerialization;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    public class Asset
    {
        [BinaryProperty(0)]
        [JsonProperty("version")]
        public byte Version;
        
        [BinaryProperty(1)]
        [JsonProperty("hash")]
        public UInt160 Hash;
        
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
