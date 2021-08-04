using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.VM
{
    public class ContractEncoder
    {
        private readonly BinaryWriter _staticBinaryWriter;
        private readonly BinaryWriter _dynamicBinaryWriter;
        private readonly dynamic[] _values;

        public ContractEncoder(string methodSignature, dynamic[] values)
        {
            _values = values;
            _staticBinaryWriter = new BinaryWriter(new MemoryStream());
            _dynamicBinaryWriter = new BinaryWriter(new MemoryStream());
            var signature = MethodSignatureAsInt(methodSignature);
            _staticBinaryWriter.Write(signature);
        }

        public static byte[] Encode(string methodSignature, params dynamic[] values)
        {
            var encoder = new ContractEncoder(methodSignature, values);
            if (values.GetType() == typeof(byte[][]))
                encoder = encoder.Write((byte[][]) values);
            else
                encoder = values.Aggregate(encoder, (current, value) => current.Write(value));

            return encoder.ToByteArray();
        }

        public static IEnumerable<byte> MethodSignature(string method)
        {
            return Encoding.ASCII.GetBytes(method).KeccakBytes();
        }

        public static uint MethodSignatureAsInt(string method)
        {
            return MethodSignatureAsInt(MethodSignature(method));
        }

        public static uint MethodSignatureAsInt(IEnumerable<byte> bytes)
        {
            return bytes.Take(4)
                .Select(((b, i) => (uint) b << (8 * i)))
                .Aggregate((x, y) => x | y);
        }

        public static dynamic TypedValueFromString(string v)
        {
            if (v.StartsWith("0x")) // hex value
            {
                return v.Length switch
                {
                    // UInt160
                    42 => v.HexToUInt160(),
                    // UInt256
                    66 => v.HexToUInt256(),
                    _ => v.HexToBytes()
                };
            }

            if (v.StartsWith("\"") && v.EndsWith("\"")) // string
            {
                return v.Substring(1, v.Length - 2);
            }

            int intValue;
            if (int.TryParse(v, out intValue)) // numeric
                return intValue;
            
            throw new InvalidOperationException();
        }
        
        public static dynamic[] RestoreTypesFromStrings(IEnumerable<string> values)
        {
            dynamic[] result = new dynamic[values.Count()];
            var idx = 0;
            foreach (var v in values)
                result[idx++] = TypedValueFromString(v);
            return result;
        }

        public ContractEncoder Write(byte[] array, bool toDynamic = false)
        {
            var staticOffset = _values.Length * 32;
            var dynamicOffset = _dynamicBinaryWriter.BaseStream.Length;
            var offset = staticOffset + dynamicOffset;

            if (toDynamic)
                WriteDynamic(new BigInteger(offset).ToUInt256());
            else
                Write(new BigInteger(offset).ToUInt256());

            WriteDynamic(new BigInteger(array.Length).ToUInt256());
            foreach (var word in array.Batch(32))
                _dynamicBinaryWriter.Write(word.PadRight((byte) 0, 32).ToArray());
            return this;
        }

        public ContractEncoder Write(byte[][] array)
        {
            var staticOffset = _values.Length * 32;
            var dynamicOffset = _dynamicBinaryWriter.BaseStream.Length;
            var offset = staticOffset + dynamicOffset;
            Write(new BigInteger(offset).ToUInt256());
            WriteDynamic(new BigInteger(array.Length).ToUInt256());
            foreach (var x in array)
                Write(x, true);
            return this;
        }

        public ContractEncoder Write(UInt256[] array)
        {
            var staticOffset = _values.Length * 32;
            var dynamicOffset = _dynamicBinaryWriter.BaseStream.Length;
            var offset = staticOffset + dynamicOffset;
            Write(new BigInteger(offset).ToUInt256());
            WriteDynamic(new BigInteger(array.Length).ToUInt256());
            foreach (var x in array)
                WriteDynamic(x);
            return this;
        }

        public ContractEncoder Write(UInt256 value)
        {
            _staticBinaryWriter.Write(value.ToBytes().Reverse().ToArray());
            return this;
        }

        public ContractEncoder WriteDynamic(UInt256 value)
        {
            _dynamicBinaryWriter.Write(value.ToBytes().Reverse().ToArray());
            return this;
        }

        public ContractEncoder Write(UInt160 value)
        {
            // encode uint160 as 32 byte zero padded
            _staticBinaryWriter.Write(new byte[12]);
            _staticBinaryWriter.Write(value.ToBytes().ToArray());
            return this;
        }

        public ContractEncoder Write(Money value)
        {
            Write(value.ToUInt256());
            return this;
        }

        public byte[] ToByteArray()
        {
            var staticPart = (_staticBinaryWriter.BaseStream as MemoryStream)?.ToArray() ??
                             throw new InvalidOperationException();
            var dynamicPart = (_dynamicBinaryWriter.BaseStream as MemoryStream)?.ToArray() ??
                              throw new InvalidOperationException();
            return staticPart.Concat(dynamicPart).ToArray();
        }
    }
}