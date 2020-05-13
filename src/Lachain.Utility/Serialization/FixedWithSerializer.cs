using System;
using System.Collections.Generic;
using System.Linq;
using Lachain.Utility.Utils;

namespace Lachain.Utility.Serialization
{
    public class FixedWithSerializer
    {
        public static byte[] Serialize(params dynamic[] args)
        {
            var buffer = new byte[args.Sum(arg => SerializationUtils.GetTypeWidth(arg.GetType()))];
            SerializeToMemory(buffer, args);
            return buffer;
        }
        
        public static void SerializeToMemory(Memory<byte> bytes, dynamic[] args)
        {
            var offset = 0;
            foreach (var arg in args)
            {
                var sz = SerializationUtils.GetTypeWidth(arg.GetType());
                SerializationUtils.SerializeType(bytes.Slice(offset, sz), arg);
                offset += sz;
            }
        }


        public static byte[] SerializeArray(IReadOnlyCollection<IFixedWidth> args)
        {
            var buffer = new byte[args.Sum(arg => arg.Size())];
            var offset = 0;
            foreach (var arg in args)
            {
                var sz = arg.Size();
                arg.Serialize(buffer.AsMemory().Slice(offset, sz));
                offset += sz;
            }

            return buffer;
        }

        public static dynamic[] Deserialize(ReadOnlyMemory<byte> bytes, out int offset, params Type[] types)
        {
            return DeserializeArray(bytes, out offset, types);
        }

        public static dynamic[] DeserializeArray(ReadOnlyMemory<byte> bytes, out int offset, Type[] types)
        {
            offset = 0;
            var result = new dynamic[types.Length];
            foreach (var (type, i) in types.WithIndex())
            {
                var sz = SerializationUtils.GetTypeWidth(type);
                result[i] = SerializationUtils.DeserializeType(bytes.Slice(offset, sz), type);
                offset += sz;
            }

            return result;
        }

        public static T[] DeserializeHomogeneous<T>(ReadOnlyMemory<byte> bytes) where T : IFixedWidth
        {
            var elementSz = SerializationUtils.FixedWidthSize(typeof(T));
            if (bytes.Length % elementSz != 0)
                throw new InvalidOperationException($"Cannot deserialize {typeof(T)}: wrong size");
            var result = new T[bytes.Length / elementSz];
            for (var i = 0; i < result.Length; ++i)
            {
                result[i] = (T) (typeof(T).GetMethod("FromBytes") ?? throw new InvalidOperationException())
                    .Invoke(null, new object[] {bytes.Slice(i * elementSz, elementSz)});
            }

            return result;
        }
    }
}