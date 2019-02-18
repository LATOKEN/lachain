using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Phorkus.Proto;
using Phorkus.Utility.Utils;

namespace Phorkus.Core.VM
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
                    default:
                        throw new ArgumentOutOfRangeException(nameof(type), "Unsupported type in method siganture (" + type + ")");
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
    }
}