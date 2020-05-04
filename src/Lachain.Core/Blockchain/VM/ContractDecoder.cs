using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lachain.Core.Blockchain.Error;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Core.Blockchain.VM
{
    public class ContractDecoder
    {
        private readonly BinaryReader _binaryReader;
        private readonly byte[] _input;

        public ContractDecoder(byte[] input)
        {
            _binaryReader = new BinaryReader(new MemoryStream(input));
            _input = input;
        }

        public object[] Decode(string signature)
        {
            var parts = signature.Split('(');
            if (parts.Length != 2)
                throw new ContractAbiException("Unable to parse ABI method signature (" + signature.Length + ")");
            if (_binaryReader.ReadUInt32() != ContractEncoder.MethodSignatureAsInt(signature))
                throw new ContractAbiException("Decoded ABI does not match method signature");
            parts[1] = parts[1].TrimEnd(')');
            if (parts[1] == "")
            {
                return new object[]{};
            }
            var types = parts[1].Split(',');
            var result = new List<object>(types.Length);
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
                _ => throw new ContractAbiException("Unsupported type in method signature (" + type + ")")
            }));

            return result.ToArray();
        }

        private byte[] DecodeBytes()
        {
            var dynamicOffset = DecodeUInt256().ToBigInteger(true) + 4;
            if (dynamicOffset > int.MaxValue)
                throw new ContractAbiException("Encoded array length is too long");
            var len = (int)_input.Skip((int) dynamicOffset).Take(32).ToArray().ToUInt256().ToBigInteger(true);
            var res = _input.Skip((int) (dynamicOffset + 32)).Take(len).ToArray();
            return res;
        }

        private UInt256 DecodeUInt256()
        {
            return _binaryReader.ReadBytes(32).ToUInt256();
        }

        private UInt160 DecodeUInt160()
        {
            // decode uint160 as 32 byte zero padded
            return _binaryReader.ReadBytes(32).Skip(12).ToArray().ToUInt160();
        }

        private byte[][] DecodeBytesList()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ContractAbiException("Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => DecodeBytes())
                .ToArray();
        }

        private UInt160[] DecodeUInt160List()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ContractAbiException("Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => DecodeUInt160())
                .ToArray();
        }
    }
}