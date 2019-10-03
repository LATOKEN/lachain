using System.IO;
using System.Linq;
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
            encoder = values.Aggregate(encoder,
                (current, value) => current.Write(value));
            return encoder.ToByteArray();
        }
        
        public ContractEncoder(string methodSignature)
        {
            _binaryWriter = new BinaryWriter(
                new MemoryStream());
            var buffer = Encoding.ASCII.GetBytes(methodSignature)
                .Keccak256();
            var signature = 0;
            if (!methodSignature.StartsWith("constructor("))
                signature = buffer[0] | buffer[1] << 8 | buffer[2] << 16 | buffer[3] << 24;
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
            return (_binaryWriter.BaseStream as MemoryStream)?.ToArray();
        }
    }
}