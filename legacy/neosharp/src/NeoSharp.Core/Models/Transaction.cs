using System;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using Newtonsoft.Json;

namespace NeoSharp.Core.Models
{
    [Serializable]
    [BinaryTypeSerializer(typeof(TransactionSerializer))]
    public class Transaction
    {
        /* TODO: "don't serialize hash" */
        [BinaryProperty(0)]
        [JsonProperty("hash")]
        public UInt256 Hash { get; set; }
        
        [BinaryProperty(1)]
        [JsonProperty("type")]
        public TransactionType Type { get; }

        [BinaryProperty(2)]
        [JsonProperty("version")]
        public byte Version { get; set; }
        
        [BinaryProperty(3)]
        [JsonProperty("flags")]
        public TransactionFlags Flags { get; set; } = TransactionFlags.None;
        
        [BinaryProperty(4)]
        [JsonProperty("nonce")]
        public ulong Nonce { get; set; }
        
        /* TODO: "rethink it" */
        [BinaryProperty(5)]
        [JsonProperty("from")]
        public UInt160 From { get; set; }
        
        /// <summary>
        /// Constructor
        /// </summary>
        public Transaction()
        {
            // Just for serialization
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="type">Type</param>
        protected Transaction(TransactionType type)
        {
            Type = type;
        }

        /// <summary>
        /// Deserialize logic
        /// </summary>
        /// <param name="deserializer">Deserializer</param>
        /// <param name="reader">Reader</param>
        /// <param name="settings">Settings</param>
        public void Deserialize(IBinarySerializer deserializer, BinaryReader reader,
            BinarySerializerSettings settings = null)
        {
            /* transcation type already readed */
            Version = reader.ReadByte(); /* 1 bytes */
            Flags = (TransactionFlags) reader.ReadUInt32(); /* 4 bytes */
            Nonce = reader.ReadUInt64(); /* 8 bytes */
            From = new UInt160(reader.ReadBytes(20)); /* 20 bytes (160 bits) */
            
            DeserializeExclusiveData(deserializer, reader, settings);
        }

        /// <summary>
        /// Serialize logic
        /// </summary>
        /// <param name="serializer">Serializer</param>
        /// <param name="writer">Writer</param>
        /// <param name="settings">Settings</param>
        /// <returns>How many bytes have been written</returns>
        public int Serialize(IBinarySerializer serializer, BinaryWriter writer,
            BinarySerializerSettings settings = null)
        {
            var result = 0;
            
            writer.Write((byte) Type); /* 1 byte */
            result += 1;
            writer.Write(Version); /* 1 byte */
            result += 1;
            writer.Write((uint) Flags); /* 4 bytes */
            result += 4;
            writer.Write(Nonce); /* 8 bytes */
            result += 8;
            writer.Write(From.ToArray()); /* 20 bytes (160 bits) */
            result += 20;
            
            result += SerializeExclusiveData(serializer, writer, settings);
            return result;
        }

        /// <summary>
        /// Deserialize logic
        /// </summary>
        /// <param name="deserializer">Deserializer</param>
        /// <param name="reader">Reader</param>
        /// <param name="settings">Settings</param>
        /// <returns>How many bytes have been written</returns>
        protected virtual void DeserializeExclusiveData(IBinarySerializer deserializer, BinaryReader reader,
            BinarySerializerSettings settings = null)
        {
        }

        /// <summary>
        /// Serialize logic
        /// </summary>
        /// <param name="serializer">Serializer</param>
        /// <param name="writer">Writer</param>
        /// <param name="settings">Settings</param>
        /// <returns>How many bytes have been written</returns>
        protected virtual int SerializeExclusiveData(IBinarySerializer serializer, BinaryWriter writer,
            BinarySerializerSettings settings = null)
        {
            return 0;
        }
    }
}