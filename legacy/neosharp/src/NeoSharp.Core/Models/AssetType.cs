using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;

namespace NeoSharp.Core.Models
{
    [Serializable]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum AssetType : byte
    {
        /// <summary>
        /// Governing token for shares
        /// </summary>
        GoverningToken = 0x00,
        
        /// <summary>
        /// Platform tokens for cross-chain integration
        /// </summary>
        PlatformToken = 0x02,
        
        /// <summary>
        /// Customer tokens that can be published by users
        /// </summary>
        CustomToken = 0x03
    }
}
