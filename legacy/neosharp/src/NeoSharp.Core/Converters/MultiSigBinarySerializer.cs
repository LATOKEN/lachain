using System;
using System.IO;
using NeoSharp.BinarySerialization;
using NeoSharp.Core.Cryptography;

namespace NeoSharp.Core.Converters
{
    public class MultiSigBinarySerializer : IBinaryCustomSerializable
    {
        public object Deserialize(IBinarySerializer deserializer, BinaryReader reader, Type type,
            BinarySerializerSettings settings = null)
        {
            var multisig = new MultiSig();
            multisig.Deserialize(deserializer, reader);
            return multisig;
        }

        public int Serialize(IBinarySerializer serializer, BinaryWriter writer, object value,
            BinarySerializerSettings settings = null)
        {
            if (!(value is MultiSig multisig))
                throw new ArgumentException(nameof(value));
            
            return multisig.Serialize(serializer, writer);
        }
    }
}