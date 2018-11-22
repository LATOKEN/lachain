using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Phorkus.Core.Cryptography;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
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

        public static ICollection<T> ToMessageArray<T>(this byte[] buffer, ulong limit = ulong.MaxValue)
            where T : IMessage<T>, new()
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var array = new T[length];
                var parser = new MessageParser<T>(() => new T());
                for (var i = 0UL; i < length; i++)
                    array[i] = parser.ParseFrom(stream);
                return array;
            }
        }

        public static UInt256 ToHash256<T>(this T t)
            where T : IMessage<T>
        {
            return new UInt256
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Sha256())
            };
        }

        public static UInt160 ToHash160<T>(this T t)
            where T : IMessage<T>
        {
            return new UInt160
            {
                Buffer = ByteString.CopyFrom(t.ToByteArray().Ripemd160())
            };
        }
    }
}