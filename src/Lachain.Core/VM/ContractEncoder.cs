using System;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility;

namespace Lachain.Core.VM
{
    public class ContractEncoder
    {
        private readonly BinaryWriter _binaryWriter;

        public static byte[] Encode(string methodSignature, params dynamic[] values)
        {
            var encoder = new ContractEncoder(methodSignature);
            encoder = values.Aggregate(encoder,
                (current, value) => current.Write(value));
            return encoder.ToByteArray();
        }

        public static uint MethodSignatureBytes(string methodSignature)
        {
            var buffer = Encoding.ASCII.GetBytes(methodSignature).KeccakBytes();
            if (methodSignature.StartsWith("constructor(")) return 0;
            return buffer[0] | ((uint) buffer[1] << 8) | ((uint) buffer[2] << 16) | ((uint) buffer[3] << 24);
        }

        public ContractEncoder(string methodSignature)
        {
            _binaryWriter = new BinaryWriter(
                new MemoryStream());
            var signature = MethodSignatureBytes(methodSignature);
            _binaryWriter.Write(signature);
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

        public ContractEncoder Write(long value)
        {
            _binaryWriter.Write(value);
            return this;
        }

        public ContractEncoder Write(UInt256 value)
        {
            var buffer = value.Buffer.ToByteArray();
            _binaryWriter.Write(buffer);
            return this;
        }

        public ContractEncoder Write(UInt160 value)
        {
            var buffer = value.Buffer.ToByteArray();
            _binaryWriter.Write(buffer);
            return this;
        }

        public ContractEncoder Write(Money value)
        {
            var buffer = value.ToUInt256().Buffer.ToByteArray();
            _binaryWriter.Write(buffer);
            return this;
        }

        public byte[] ToByteArray()
        {
            return (_binaryWriter.BaseStream as MemoryStream)?.ToArray() ?? throw new InvalidOperationException();
        }
    }
}