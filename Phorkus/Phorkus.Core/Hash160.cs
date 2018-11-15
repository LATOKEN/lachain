using System;

namespace Phorkus.Core
{
    public class Hash160
    {
        private readonly byte[] _bytes;

        public Hash160(byte[] bytes)
        {
            if (bytes.Length != 20)
                throw new ArgumentOutOfRangeException(nameof(bytes));
            _bytes = bytes;
        }

        public byte[] ToByteArray()
        {
            return _bytes;
        }
        
        public string ToHexString()
        {
            return null;
        }
    }
}