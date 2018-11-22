using System;
using Newtonsoft.Json;

namespace NeoSharp.Core.Wallet.NEP6
{
    public class Nep6WalletParameterConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer) => serializer.Deserialize<Nep6WalletParameter[]>(reader);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            serializer.Serialize(writer, value);
    }

    public class Nep6WalletParameter : IWalletParameter
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}