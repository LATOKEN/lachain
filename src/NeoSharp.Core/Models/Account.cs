using System;
using System.Collections.Generic;
using NeoSharp.BinarySerialization;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    [Serializable]
    public class Account
    {
        [BinaryProperty(1)]
        [JsonProperty("address")]
        public UInt160 Address { get; set; }

        [BinaryProperty(2)]
        [JsonProperty("state")]
        public AccountState State { get; set; } = AccountState.Active;

        [BinaryProperty(3)]
        [JsonProperty("balances")]
        public Dictionary<UInt160, UInt256> Balances { get; set; } = new Dictionary<UInt160, UInt256>();

        public Account()
        {
        }
        
        public Account(UInt160 address) : this()
        {
            Address = address;
        }
    }
}