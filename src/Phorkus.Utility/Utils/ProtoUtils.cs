using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Google.Protobuf;

namespace Phorkus.Utility.Utils
{
    public static class ProtoUtils
    {
        public static byte[] ToByteArray<T>(this IEnumerable<T> values)
            where T : IMessage<T>
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var arrayOf = values as T[] ?? values.ToArray();
                writer.WriteLength(arrayOf.Length);
                foreach (var value in arrayOf)
                    writer.Write(value.ToByteArray());
                writer.Flush();
                return stream.ToArray();
            }
        }
        
        public static string ParsedObject(object obj)
        {
            var parsedObject = "";
            foreach(PropertyDescriptor descriptor in TypeDescriptor.GetProperties(obj))
            {
                var name = descriptor.Name;
                var value = descriptor.GetValue(obj);
                parsedObject += $"{name} : {value}";
            }
            return parsedObject;
        }

        public static ICollection<T> ToMessageArray<T>(this byte[] buffer, ulong limit = ulong.MaxValue)
            where T : IMessage<T>, new()
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var result = new List<T>();
                var parser = new MessageParser<T>(() => new T());
                for (var i = 0UL; i < length; i++)
                    result.Add(parser.ParseFrom(stream));
                return result;
            }
        }
    }
}