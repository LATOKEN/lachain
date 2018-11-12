using System;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.BinarySerialization.SerializationHooks;
using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Converters
{
    public class SignatureBinarySerializer : IBinaryCustomSerializable
    {
        public object Deserialize(IBinarySerializer deserializer, BinaryReader reader, Type type,
            BinarySerializerSettings settings = null)
        {
            var length = reader.ReadInt32();
            var bytes = reader.ReadBytes(length);
            return new Signature(bytes);
        }
        
        public int Serialize(IBinarySerializer serializer, BinaryWriter writer, object value,
            BinarySerializerSettings settings = null)
        {
            if (!(value is Signature signature))
                throw new ArgumentException(nameof(value));

            var result = 0;
            
            writer.Write(signature.Bytes.Length);
            result += 4;
            writer.Write(signature.Bytes);
            result += signature.Bytes.Length;

            return result;
        }
    }
}