using System;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using NeoSharp.Types;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models.Transactions
{
    [BinaryTypeSerializer(typeof(TransactionSerializer))]
    public class ContractTransaction : Transaction
    {   
        [BinaryProperty(5)]
        [JsonProperty("asset")]
        public UInt160 Asset { get; set; }
        
        [BinaryProperty(6)]
        [JsonProperty("to")]
        public UInt160 To { get; set; }
        
        [BinaryProperty(7)]
        [JsonProperty("value")]
        public UInt256 Value { get; set; }
        
        [BinaryProperty(100, MaxLength = 65536)]
        [JsonProperty("script")]
        public byte[] Script { get; set; } = new byte[0];
        
        /// <inheritdoc />
        public ContractTransaction() : base(TransactionType.ContractTransaction)
        {
        }

        protected override void DeserializeExclusiveData(IBinarySerializer deserializer, BinaryReader reader,
            BinarySerializerSettings settings = null)
        {
            /* byte already readed by (1 bytes) */
            To = new UInt160(reader.ReadBytes(20)); /* 20 bytes (160 bits) */
            Asset = new UInt160(reader.ReadBytes(20)); /* 20 bytes (160 bits) */
            Value = new UInt256(reader.ReadBytes(32)); /* 32 bytes (256 bits) */
            Nonce = reader.ReadUInt32(); /* 4 bytes */
            
            var scriptLength = reader.ReadInt32();
            if (scriptLength < 0 || scriptLength > 65536)
                throw new FormatException("Invalid script length specified, it must be non negative and greater that 64kb");
            if (scriptLength > 0)
                Script = reader.ReadBytes(scriptLength);
        }
        
        protected override int SerializeExclusiveData(IBinarySerializer serializer, BinaryWriter writer, BinarySerializerSettings settings = null)
        {
            var result = 0;
            
            writer.Write(To.ToArray()); /* 20 bytes (160 bits) */
            result += 20;
            writer.Write(Asset.ToArray()); /* 20 bytes (160 bits) */
            result += 20;
            writer.Write(Value.ToArray()); /* 32 bytes (256 bits) */
            result += 32;
            writer.Write(Nonce); /* 4 bytes */
            result += 4;
            writer.Write(Script?.Length ?? 0); /* 4 bytes */
            result += 4;
            writer.Write(Script); /* [Script.Length] bytes */
            result += Script?.Length ?? 0;
            
            return result;
        }
    }
}