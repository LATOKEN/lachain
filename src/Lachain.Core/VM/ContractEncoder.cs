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

namespace Lachain.Core.VM
{
    public class ContractEncoder
    {
        private readonly BinaryWriter _binaryWriter;

        public ContractEncoder(string methodSignature)
        {
            _binaryWriter = new BinaryWriter(new MemoryStream());
            var signature = MethodSignatureAsInt(methodSignature);
            _binaryWriter.Write(signature);
        }

        public static byte[] Encode(string methodSignature, params dynamic[] values)
        {
            var encoder = new ContractEncoder(methodSignature);
            encoder = values.Aggregate(encoder,
                (current, value) => current.Write(value));
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

        public ContractEncoder Write(byte[] array)
        {
            Write(new BigInteger(array.Length).ToUInt256());
            foreach (var word in array.Batch(32))
                _binaryWriter.Write(word.PadRight((byte) 0, 32).ToArray());
            return this;
        }

        public ContractEncoder Write(byte[][] array)
        {
            Write(new BigInteger(array.Length).ToUInt256());
            foreach (var x in array)
                Write(x);
            return this;
        }

        public ContractEncoder Write(bool value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(short value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(int value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(uint value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(long value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(UInt256 value)
        {
            _binaryWriter.Write(value.ToBytes());
            return this;
        }

        public ContractEncoder Write(UInt160 value)
        {
            _binaryWriter.Write(value.ToBytes());
            return this;
        }

        public ContractEncoder Write(Money value)
        {
            _binaryWriter.Write(value.ToUInt256().ToBytes());
            return this;
        }

        public byte[] ToByteArray()
        {
            return (_binaryWriter.BaseStream as MemoryStream)?.ToArray() ?? throw new InvalidOperationException();
        }
    }
}