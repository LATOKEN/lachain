using NeoSharp.Types.ExtensionMethods;

namespace NeoSharp.Core.Cryptography
{
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