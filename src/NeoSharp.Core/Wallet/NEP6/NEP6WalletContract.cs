using System;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Wallet.NEP6
{
    public class Nep6WalletContractConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => true;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer) => serializer.Deserialize<Nep6WalletContract>(reader);
        
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) =>
            serializer.Serialize(writer, value);
    }

    public class Nep6WalletContract : IWalletContract
    {
        [JsonIgnore]
        public UInt160 ScriptHash => UInt160.Parse(Script);

        [JsonProperty("script")]
        public string Script { get; set; }
        
        [JsonConverter(typeof(Nep6WalletParameterConverter))]
        [JsonProperty("parameters")]
        public IWalletParameter[] Parameters { get; set; }
    }
}