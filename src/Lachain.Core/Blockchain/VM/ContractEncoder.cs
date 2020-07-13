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
            if (values.GetType() == typeof (byte[][]))
            {
                encoder = encoder.Write((byte[][]) values);
            }
            else
            {
                encoder = values.Aggregate(encoder,
                    (current, value) => { return current.Write(value); });
            }

            return encoder.ToByteArray();
        }

        public static uint MethodSignatureAsInt(string methodSignature)
        {
            return MethodSignatureAsInt(Encoding.ASCII.GetBytes(methodSignature).KeccakBytes());
        }

        public static uint MethodSignatureAsInt(IEnumerable<byte> bytes)
        {
            return bytes.Take(4)
                .Select(((b, i) => (uint) b << (8 * i)))
                .Aggregate((x, y) => x | y);
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

        public ContractEncoder Write(bool value)
        {
            _staticBinaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(short value)
        {
            _staticBinaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(int value)
        {
            _staticBinaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(uint value)
        {
            _staticBinaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(long value)
        {
            _staticBinaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(UInt256 value)
        {
            _staticBinaryWriter.Write(value.ToBytes().ToArray());
            return this;
        }

        public ContractEncoder WriteDynamic(UInt256 value)
        {
            _dynamicBinaryWriter.Write(value.ToBytes().ToArray());
            return this;
        }

        public ContractEncoder Write(UInt160 value)
        {
            // encode uint160 as 32 byte zero padded
            _staticBinaryWriter.Write(new byte[12]);
            _staticBinaryWriter.Write(value.ToBytes());
            return this;
        }

        public ContractEncoder Write(Money value)
        {
            _staticBinaryWriter.Write(value.ToUInt256().ToBytes());
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