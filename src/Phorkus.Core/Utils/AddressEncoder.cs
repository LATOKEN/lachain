using System;
using Phorkus.Crypto;
using Phorkus.Proto;

namespace Phorkus.Core.Utils
{
    public static class AddressEncoder
    {
        public static byte[] EncodeAddress(AddressFormat addressFormat, byte[] publicKey)
        {
            switch (addressFormat)
            {
                case AddressFormat.Ripmd160:
                    return publicKey.Ripemd160();
                case AddressFormat.Ed25519:
                    return publicKey.Ed25519();
                default:
                    throw new ArgumentOutOfRangeException(nameof(addressFormat), addressFormat, null);
            }
        }
    }
}