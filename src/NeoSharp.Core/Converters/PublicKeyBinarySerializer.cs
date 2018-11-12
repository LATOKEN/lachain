using System;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.BinarySerialization.SerializationHooks;
using NeoSharp.Core.Cryptography;
using NeoSharp.Core.Exceptions;

namespace NeoSharp.Core.Converters
{
    public class PublicKeyBinarySerializer : IBinaryCustomSerializable
    {
        private const int ExpectedLength = 32; /* (curve.Q.GetBitLength() + 7) / 8; */
        
        public object Deserialize(IBinarySerializer binaryDeserializer, BinaryReader reader, Type type, BinarySerializerSettings settings = null)
        {
            var prefix = reader.ReadByte();

            switch (prefix)
            {
                case 0x00: return PublicKey.Infinity;
                case 0x02:
                case 0x03:
                    {
                        byte[] buffer = new byte[1 + ExpectedLength];
                        buffer[0] = prefix;

                        reader.Read(buffer, 1, ExpectedLength);
                        return new PublicKey(buffer);
                    }
                case 0x04:
                case 0x06:
                case 0x07:
                    {
                        byte[] buffer = new byte[1 + ExpectedLength * 2];
                        buffer[0] = prefix;

                        reader.Read(buffer, 1, ExpectedLength * 2);
                        return new PublicKey(buffer);
                    }
                default: throw new InvalidECPointException("Invalid point encoding " + prefix);
            }
        }

        public int Serialize(IBinarySerializer binarySerializer, BinaryWriter writer, object value, BinarySerializerSettings settings = null)
        {
            var ec = (PublicKey)value;

            writer.Write(ec.EncodedData, 0, ec.EncodedData.Length);
            return ec.EncodedData.Length;
        }
    }
}