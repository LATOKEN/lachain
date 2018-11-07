using System;
using System.Collections.Generic;
using NeoSharp.Core.Models;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Wallet.NEP6
{
    public class Nep6AccountConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer) => serializer.Deserialize<Nep6Account[]>(reader);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            serializer.Serialize(writer, value);
    }

    public class Nep6Account : IWalletAccount, IEquatable<Nep6Account>
    {
        /// <inheritdoc />
        [JsonProperty("address")]
        public string Address { get; set; }
        
        [JsonIgnore]
        public UInt160 ScriptHash => UInt160.Parse(Address);
        
        /// <inheritdoc />
        [JsonProperty("label")]
        public string Label { get; set; }

        /// <inheritdoc />
        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }

        /// <inheritdoc />
        [JsonProperty("lock")]
        public bool Lock { get; set; }

        /// <inheritdoc />
        [JsonProperty("key")]
        public string Key { get; set; }

        /// <inheritdoc />
        [JsonConverter(typeof(Nep6WalletContractConverter))]
        [JsonProperty("contract")]
        public IWalletContract Contract { get; set; }

        [JsonProperty("extra")] public IDictionary<string, string> Extra { get; set; }

        public Nep6Account()
        {
        }

        public Nep6Account(Contract contract)
        {
            Address = contract.ScriptHash.ToString();
        }
        
        public Nep6Account(IWalletContract accountContract)
        {
            Contract = accountContract;
        }

        public bool Equals(Nep6Account obj)
        {
            return GetHashCode() == obj.GetHashCode();
        }
    }
}