using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lachain.Crypto;
using Lachain.Proto;
using Lachain.Utility.Utils;

namespace Lachain.Storage.Repositories
{
    public class ContractDecoderLtr
    {
        private BinaryReader _binaryReaderStatic;
        private BinaryReader _binaryReaderDynamic;
        private readonly byte[] _inputData;

        public ContractDecoderLtr(byte[] input)
        {
            _binaryReaderStatic = new BinaryReader(new MemoryStream(input));
            _binaryReaderDynamic = new BinaryReader(Stream.Null);
            _inputData = input;
        }

        public object[] Decode(string signature)
        {
            var parts = signature.Split('(');
           if (parts.Length == 1)
                return DecodeSimpleTypes(signature);
            if (parts.Length == 2)
            {
                if (_binaryReaderStatic.ReadUInt32() != MethodSignatureAsInt(signature))
                    throw new InvalidOperationException("Decoded ABI does not match method signature");
            }
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
                _ => throw new InvalidOperationException("Unsupported type in method signature (" + type + ")")
            }));

            return result.ToArray();
        }
        
        private object[] DecodeSimpleTypes(string signature)
        {
            if (signature == "")
            {
                return new object[]{};
            }
            var types = signature.Split(',');
            var result = new List<object>(types.Length);
            var staticInput = _inputData.Take(types.Length * 32).ToArray();
            _binaryReaderStatic = new BinaryReader(new MemoryStream(staticInput));
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
                _ => throw new InvalidOperationException("Unsupported type in method signature (" + type + ")")
            }));

            return result.ToArray();
        }

        private byte[] DecodeBytes(bool readFromDynamic = false)
        {
            var offset = DecodeUInt256(readFromDynamic).ToBigInteger();
            if (offset > int.MaxValue)
                throw new InvalidOperationException("Offset is too big");
            var lenUint = DecodeUInt256(true);
            var len = (int) lenUint.ToBigInteger();
            var words = len % 32 == 0 ? len / 32 : len / 32 + 1;
            var res = _binaryReaderDynamic.ReadBytes(words * 32).Take(len).ToArray();
            return res;
        }

        private UInt256 DecodeUInt256(bool readFromDynamic = false)
        {
            return readFromDynamic
                ? _binaryReaderDynamic.ReadBytes(32).ToUInt256()
                : _binaryReaderStatic.ReadBytes(32).ToUInt256();
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
                throw new InvalidOperationException("Offset is too large");
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
                throw new InvalidOperationException("Offset is too big");
            var lenOfArray = (int) DecodeUInt256(true).ToBigInteger();
            
            return Enumerable.Range(1, lenOfArray)
                .Select(_ => DecodeUInt256(true))
                .ToArray();
        }

        private UInt160[] DecodeUInt160List()
        {
            var len = DecodeUInt256().ToBigInteger();
            if (len > int.MaxValue)
                throw new InvalidOperationException("Encoded array length is too long");
            return Enumerable.Range(0, (int) len)
                .Select(_ => DecodeUInt160())
                .ToArray();
        }
        
        public static IEnumerable<byte> MethodSignature(string method)
        {
            return Encoding.ASCII.GetBytes(method).KeccakBytes();
        }
        
        public static uint MethodSignatureAsInt(string method)
        {
            return MethodSignatureAsInt(MethodSignature(method));
        }

        public static uint MethodSignatureAsInt(IEnumerable<byte> bytes)
        {
            return bytes.Take(4)
                .Select(((b, i) => (uint) b << (8 * i)))
                .Aggregate((x, y) => x | y);
        }
    }
}