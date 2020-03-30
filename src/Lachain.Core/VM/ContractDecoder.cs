using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Lachain.Core.Blockchain.ContractManager;
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

        public ContractDecoder(BinaryReader binaryReader)
        {
            _binaryReader = binaryReader;
        }

        public object[] Decode(string signature)
        {
            var regExp = new Regex("[\\w\\d_]+\\(([\\w\\d_,]+)\\)");
            var matches = regExp.Matches(signature);
            if (matches.Count == 0)
                throw new Exception("Unable to parse ABI method signature (" + signature.Length + ")");
            var types = matches[1].Value.Split(',');
            var result = new List<object>(types.Length);
            if (_binaryReader.ReadUInt32() != ContractEncoder.MethodSignatureBytes(signature))
                throw new ArgumentException("Decoded ABI does not match method signature");                
            foreach (var type in types)
            {
                object value;
                switch (type)
                {
                    case "uint256":
                    case "uint":
                        value = DecodeUInt256();
                        break;
                    case "address":
                    case "uint160":
                        value = DecodeUInt160();
                        break;
                    case "address[]":
                    case "uint160[]":
                        value = DecodeUInt160List();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type),
                            "Unsupported type in method signature (" + type + ")");
                }

                result.Add(value);
            }

            return result.ToArray();
        }

        private UInt256 DecodeUInt256()
        {
            return _binaryReader.ReadBytes(32).ToUInt256();
        }

        private UInt160 DecodeUInt160()
        {
            return _binaryReader.ReadBytes(20).ToUInt160();
        }

        private UInt160[] DecodeUInt160List()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(len), "Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => _binaryReader.ReadBytes(20))
                .Select(x => x.ToUInt160())
                .ToArray();
        }
    }
}