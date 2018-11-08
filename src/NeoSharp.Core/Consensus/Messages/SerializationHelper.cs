using System.IO;
using NeoSharp.BinarySerialization;

namespace NeoSharp.Core.Consensus.Messages
{
    /*
     * TODO: refactor this to use BinarySerializer.Default
     */
    public class SerializationHelper
    {
        public static T[] ReadSerializableArray<T>(BinaryReader reader, int max = 0x1000000)
        {
            var array = new T[reader.ReadVarInt((ulong)max)];
            for (var i = 0; i < array.Length; i++)
            {
                array[i] = BinarySerializer.Default.Deserialize<T>(reader);
            }
            return array;
        }
        
        public static void WriteSerializableArray<T>(BinaryWriter writer, T[] value)
        {
            writer.WriteVarInt(value.Length);
            foreach (var t in value)
            {
                writer.Write(BinarySerializer.Default.Serialize(t));
            }
        }
    }
}