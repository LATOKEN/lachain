using System.IO;
using System.Text;
using Phorkus.Crypto;
using Phorkus.Proto;
using Phorkus.Utility;

namespace Phorkus.Core.VM
{
    public class ContractEncoder
    {
        private readonly BinaryWriter _binaryWriter;

        public static byte[] Encode(string methodSignature, params dynamic[] values)
        {
            var encoder = new ContractEncoder(methodSignature);
            foreach (var value in values)
                encoder.Write(value);
            return encoder.ToByteArray();
        }
        
        public ContractEncoder(string methodSignature)
        {
            _binaryWriter = new BinaryWriter(
                new MemoryStream());
            var buffer = Encoding.ASCII.GetBytes(methodSignature)
                .ToKeccak256();
            var signature = buffer[3] | buffer[2] << 8 | buffer[1] << 16 | buffer[0] << 24;
            _binaryWriter.Write(signature);
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
            return (_binaryWriter.BaseStream as MemoryStream)?.ToArray();
        }
    }
}