using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Lachain.Proto;

namespace Lachain.Utility.Utils
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
                {
                    var valueByteArray = value.ToByteArray();
                    writer.WriteLength(valueByteArray.Length);
                    writer.Write(valueByteArray);
                }
                writer.Flush();
                return stream.ToArray();
            }
        }
        
        public static byte[] TransactionHashListToByteArray(this IEnumerable<UInt256> values)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                var arrayOf = values.ToArray();
                var len = 0;
                
                foreach (var value in arrayOf) 
                {
                    if(value.ToByteArray().Length == 34) 
                    {
                        len++;
                    }
                }
                writer.WriteLength(len);
                foreach (var value in arrayOf)
                {
                    if(value.ToByteArray().Length == 34) 
                    {
                        writer.Write(value.ToByteArray());
                    }
                }
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
                {
                    var msgLen = (int) reader.ReadLength();
                    var el = parser.ParseFrom(reader.ReadBytes(msgLen));
                    result.Add(el);
                }

                return result;
            }
        }

        public static ICollection<UInt256> ByteArrayToTransactionHashList(this byte[] buffer, ulong limit = ulong.MaxValue)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var result = new List<UInt256>();
                var parser = new MessageParser<UInt256>(() => new UInt256());
                
                for (var i = 0UL; i < length; i++)
                {
                    var bytes = reader.ReadBytes(34);
                    if(bytes.Length == 34) 
                    {
                        var el = parser.ParseFrom(bytes);
                        result.Add(el);
                    }
                    else {
                        break;
                    }
                }
                return result;
            }
        }
        
        public static ICollection<ECDSAPublicKey> ToEcdsaPublicKeys(this byte[] buffer, ulong limit = ulong.MaxValue)
        {
            using (var stream = new MemoryStream(buffer))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadLength(limit);
                var result = new List<ECDSAPublicKey>();
                for (var i = 0UL; i < length; i++)
                {
                    var el = reader.ReadBytes(35).Skip(2).ToArray();
                    result.Add(new ECDSAPublicKey {Buffer = ByteString.CopyFrom(el)});
                }

                return result;
            }
        }
    }
}