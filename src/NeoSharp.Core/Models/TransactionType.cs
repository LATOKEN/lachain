using System;
using NeoSharp.Core.Caching;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NeoSharp.Core.Models
{
    [Serializable]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum TransactionType : byte
    {
        /// <summary>
        /// Transaction with miner's fee
        /// </summary>
        [ReflectionCache(typeof(MinerTransaction))]
        MinerTransaction = 0x00,

        /// <summary>
        /// Register new asset or token
        /// </summary>
        [ReflectionCache(typeof(RegisterTransaction))]
        RegisterTransaction = 0x01,
        
        /// <summary>
        /// Issue funds to asset or token
        /// </summary>
        [ReflectionCache(typeof(IssueTransaction))]
        IssueTransaction = 0x02,

        /// <summary>
        /// 
        /// </summary>
        [ReflectionCache(typeof(ContractTransaction))]
        ContractTransaction = 0x03,
        
        [ReflectionCache(typeof(PublishTransaction))]
        PublishTransaction = 0x04
    }
}