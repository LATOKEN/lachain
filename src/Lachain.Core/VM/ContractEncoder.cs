using System;
using System.IO;
using System.Linq;
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
            var signature = MethodSignatureBytes(methodSignature);
            _binaryWriter.Write(signature);
        }

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