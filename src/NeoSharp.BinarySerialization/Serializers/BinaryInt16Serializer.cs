using System;
using System.IO;

namespace NeoSharp.BinarySerialization.Serializers
{
    public class BinaryInt16Serializer : IBinaryCustomSerializable
    {
        public int Serialize(IBinarySerializer serializer, BinaryWriter writer, object value, BinarySerializerSettings settings = null)
        {
            writer.Write((short)value);
            return 2;
        }

        public object Deserialize(IBinarySerializer deserializer, BinaryReader reader, Type type, BinarySerializerSettings settings = null)
        {
            return reader.ReadInt16();
        }
    }
}