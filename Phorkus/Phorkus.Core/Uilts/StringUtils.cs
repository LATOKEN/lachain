using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Phorkus.Core.Uilts
{
    public static class StringUtils
    {
        public static byte[] ToByteArray(this IEnumerable<string> values)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var arrayOf = values as string[] ?? values.ToArray();
                writer.WriteLength(arrayOf.Length);
                foreach (var value in arrayOf)
                    writer.WriteUtf8String(value);
                writer.Flush();
                return stream.ToArray();
            }
        }
        
        public static ICollection<string> ToStringArray(this byte[] buffer, ulong limit = ulong.MaxValue)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var array = new string[length];
                for (var i = 0UL; i < length; i++)
                    array[i] = reader.ReadUtf8String();
                return array;
            }
        }
    }
}