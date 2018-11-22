using System;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    [Serializable]
    [Obsolete]
    public class Validator
    {
        [BinaryProperty(1)]
        [JsonProperty("publickey")]
        public PublicKey PublicKey;

        [BinaryProperty(2)]
        [JsonProperty("registered")]
        public bool Registered;

        [BinaryProperty(3)]
        [JsonProperty("votes")]
        public Money Votes;

        public Validator()
        {
        }

        public Validator(PublicKey publicKey)
        {
            PublicKey = publicKey;
            Registered = false;
            Votes = Money.Zero;
        }
    }
}