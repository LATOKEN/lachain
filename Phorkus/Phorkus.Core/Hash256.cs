using System;

namespace Phorkus.Core
{
    public class Hash256
    {
        private readonly byte[] _bytes;

        public Hash256(byte[] bytes)
        {
            if (bytes.Length != 32)
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