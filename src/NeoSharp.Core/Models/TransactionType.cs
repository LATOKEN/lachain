using System;
using NeoSharp.Core.Caching;
using NeoSharp.Core.Models.Transcations;
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
        RegisterTransaction = 0x40,
        
        /// <summary>
        /// Issue funds to asset or token
        /// </summary>
        [ReflectionCache(typeof(IssueTransaction))]
        IssueTransaction = 0x01,

        /// <summary>
        /// Send funds and invoke contract method
        /// </summary>
        [ReflectionCache(typeof(ContractTransaction))]
        ContractTransaction = 0x80,
        
        /// <summary>
        /// Publish new contract
        /// </summary>
        [ReflectionCache(typeof(PublishTransaction))]
        PublishTransaction = 0xd0,
        
        DepositTransaction = 0xf0,
        WithdrawTransaction = 0xf0
    }
}