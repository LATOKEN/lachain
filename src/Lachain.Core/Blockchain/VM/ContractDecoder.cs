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
        private BinaryReader _binaryReaderStatic;
        private BinaryReader _binaryReaderDynamic;
        private readonly byte[] _inputData;

        public ContractDecoder(byte[] input)
        {
            _binaryReaderStatic = new BinaryReader(new MemoryStream(input));
            _binaryReaderDynamic = new BinaryReader(Stream.Null);
            _inputData = input;
        }

        public object[] Decode(string signature)
        {
            var parts = signature.Split('(');
            if (parts.Length != 2)
                throw new ContractAbiException("Unable to parse ABI method signature (" + signature.Length + ")");
            if (_binaryReaderStatic.ReadUInt32() != ContractEncoder.MethodSignatureAsInt(signature))
                throw new ContractAbiException("Decoded ABI does not match method signature");
            parts[1] = parts[1].TrimEnd(')');
            if (parts[1] == "")
            {
                return new object[]{};
            }
            var types = parts[1].Split(',');
            var result = new List<object>(types.Length);
            var staticInput = _inputData.Skip(4).Take(types.Length * 32).ToArray();
            var dynamicInput = _inputData.Skip(4 + staticInput.Length).ToArray();
            _binaryReaderStatic = new BinaryReader(new MemoryStream(staticInput));
            _binaryReaderDynamic = new BinaryReader(new MemoryStream(dynamicInput));
            result.AddRange(types.Select(type => type switch
            {
                "uint256" => (object) DecodeUInt256(),
                "uint256[]" => DecodeUInt256List(),
                "uint" => DecodeUInt256(),
                "uint[]" => DecodeUInt256List(),
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

        private byte[] DecodeBytes(bool readFromDynamic = false)
        {
            var offset = DecodeUInt256(readFromDynamic).ToBigInteger();
            if (offset > int.MaxValue)
                throw new ContractAbiException("Offset is too big");
            var lenUint = DecodeUInt256(true);
            var len = (int) lenUint.ToBigInteger();
            var words = len % 32 == 0 ? len / 32 : len / 32 + 1;
            var res = _binaryReaderDynamic.ReadBytes(words * 32).Take(len).ToArray();
            return res;
        }

        private UInt256 DecodeUInt256(bool readFromDynamic = false)
        {
            return readFromDynamic
                ? _binaryReaderDynamic.ReadBytes(32).Reverse().ToArray().ToUInt256()
                : _binaryReaderStatic.ReadBytes(32).Reverse().ToArray().ToUInt256();
        }

        private UInt160 DecodeUInt160(bool readFromDynamic = false)
        {
            // decode uint160 as 32 byte zero padded
            return readFromDynamic
                ? _binaryReaderDynamic.ReadBytes(32).Skip(12).ToArray().ToUInt160()
                : _binaryReaderStatic.ReadBytes(32).Skip(12).ToArray().ToUInt160();
        }

        private byte[][] DecodeBytesList()
        {
            var offset = DecodeUInt256().ToBigInteger();
            if (offset > int.MaxValue)
                throw new ContractAbiException("Offset is too large");
            var lenOfArray = (int) DecodeUInt256(true).ToBigInteger();
            Enumerable.Range(1, lenOfArray)
                .Select(_ => DecodeUInt256(true).ToBigInteger());
            
            return Enumerable.Range(1, lenOfArray)
                .Select(_ => DecodeBytes(true))
                .ToArray();
        }

        private UInt256[] DecodeUInt256List()
        {
            var offset = DecodeUInt256().ToBigInteger();
            if (offset > int.MaxValue)
                throw new ContractAbiException("Offset is too big");
            var lenOfArray = (int) DecodeUInt256(true).ToBigInteger();
            
            return Enumerable.Range(1, lenOfArray)
                .Select(_ => DecodeUInt256(true))
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