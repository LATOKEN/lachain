using NeoSharp.BinarySerialization;
using NeoSharp.Core.Converters;
using NeoSharp.Types.ExtensionMethods;

namespace NeoSharp.Core.Cryptography
{
    [BinaryTypeSerializer(typeof(SignatureBinarySerializer))]
    public class Signature
    {
        public string Hex => Bytes.ToHexString();
        public byte[] Bytes { get; }

        public Signature(string hexBytes)
        {
            Bytes = hexBytes.HexToBytes();
        }

        public Signature(byte[] bytes)
        {
            Bytes = bytes;
        }
    }
}