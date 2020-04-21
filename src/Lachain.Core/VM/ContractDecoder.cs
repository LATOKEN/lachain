using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lachain.Core.Blockchain.ContractManager;
using Lachain.Core.Consensus;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.VM
{
    public class ContractDecoder
    {
        private readonly BinaryReader _binaryReader;

        public ContractDecoder(byte[] input)
        {
            _binaryReader = new BinaryReader(new MemoryStream(input));
        }

        public object[] Decode(string signature)
        {
            var parts = signature.Split('(');
            if (parts.Length != 2)
                throw new Exception("Unable to parse ABI method signature (" + signature.Length + ")");
            parts[1] = parts[1].TrimEnd(')');
            var types = parts[1].Split(',');
            var result = new List<object>(types.Length);
            if (_binaryReader.ReadUInt32() != ContractEncoder.MethodSignatureBytes(signature))
                throw new ArgumentException("Decoded ABI does not match method signature");
            result.AddRange(types.Select(type => type switch
            {
                "uint256" => (object) DecodeUInt256(),
                "uint" => DecodeUInt256(),
                "address" => DecodeUInt160(),
                "uint160" => DecodeUInt160(),
                "address[]" => DecodeUInt160List(),
                "uint160[]" => DecodeUInt160List(),
                "bytes[]" => DecodeBytesList(),
                "bytes" => DecodeBytes(),
                _ => throw new ArgumentOutOfRangeException(nameof(type), "Unsupported type in method signature (" + type + ")")
            }));

            return result.ToArray();
        }

        private byte[] DecodeBytes()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(len), "Encoded array length is too long");
            var words = ((int) len + 31) / 32;
            return _binaryReader.ReadBytes(words * 32).Take((int) len).ToArray();
        }
        
        private UInt256 DecodeUInt256()
        {
            return _binaryReader.ReadBytes(32).ToUInt256();
        }

        private UInt160 DecodeUInt160()
        {
            return _binaryReader.ReadBytes(20).ToUInt160();
        }

        private byte[][] DecodeBytesList()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(len), "Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => DecodeBytes())
                .ToArray();
        }
        
        private UInt160[] DecodeUInt160List()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(len), "Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => DecodeUInt160())
                .ToArray();
        }
    }
}